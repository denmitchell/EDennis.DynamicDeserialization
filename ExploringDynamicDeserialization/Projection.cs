using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;

namespace ExploringDynamicDeserialization {

    /// <summary>
    /// A static utility class for generating "projection" types
    /// -- types based upon an underlying type (TEntity), but
    /// which contain subsets of the properties. This class 
    /// can be used with the DynamicJsonConverter class to 
    /// properly deserialize json into dynamically typed objects.
    /// 
    /// For performance reasons (10-20 times faster), 
    /// projection types are statically cached in a concurrent 
    /// dictionary.
    ///
    /// Most of the difficult code was lifted from
    ///    https://stackoverflow.com/a/41785168
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    ///
    public class Projection<TEntity> {


        public static Dictionary<string, PropertyInfo> Properties { get; }

        private static readonly string keyBase;
        private static readonly Dictionary<string, int> propIndex;
        private static ConcurrentDictionary<string, Type> projectionTypes { get; }

        /// <summary>
        /// Statically initializes the ClassProperties via reflection
        /// </summary>
        static Projection() {
            Properties = new Dictionary<string, PropertyInfo>();
            propIndex = new Dictionary<string, int>();
            var props = typeof(TEntity).GetProperties();
            for (int i = 0; i < props.Length; i++) {
                propIndex.Add(props[i].Name.ToLower(), i);
                keyBase += props[i].Name.ToLower()[0];
                Properties.Add(props[i].Name, props[i]);
            }
            projectionTypes = new ConcurrentDictionary<string, Type>();
        }


        /// <summary>
        /// Entry point for generating a projection type.  Provide
        /// the subset of properties (property names) to be used for
        /// the projection type, and it will be generated.
        /// </summary>
        /// <param name="dynamicProps">property names (subset of TEntity property names)</param>
        /// <returns></returns>
        public static Type GetOrAddType(IEnumerable<string> dynamicProps) {
            var key = GetTypeName(dynamicProps);
            return projectionTypes.GetOrAdd(key, CreateNewType(key, dynamicProps));
        }

        /// <summary>
        /// Creates a unique name for the new type.  The name consists of 
        /// <list type="number">
        /// <item>The full name of the underlying type (TEntity)</item>
        /// <item>Underscore</item>
        /// <item>Initial letters of each property name in the underlying
        /// type (TEntity), where upper case indicates that the property 
        /// was generated in the current derived type</item>
        /// </list>
        /// and
        /// </summary>
        /// <param name="dynamicProps"></param>
        /// <returns></returns>
        private static string GetTypeName(IEnumerable<string> dynamicProps) {
            var ca = keyBase.ToCharArray();
            foreach (var dp in dynamicProps) {
                if (propIndex.TryGetValue(dp.ToLower(), out int idx))
                    ca[idx] = dp.ToUpper()[0];
            }
            return $"{typeof(TEntity).FullName}_{new string(ca)}";
        }

        /// <summary>
        /// Creates a new "projection" type with the provided key,
        /// based upon the provided list of property names
        /// 
        /// adapted from https://stackoverflow.com/a/41785168
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dynamicProps"></param>
        /// <returns></returns>
        private static Type CreateNewType(string key, IEnumerable<string> dynamicProps) {
            PropertyInfo[] properties = Properties.Where(p => dynamicProps.Any(f => f.Equals(p.Key, StringComparison.OrdinalIgnoreCase))).Select(f => f.Value).ToArray();
            var typeInfo = CompileResultTypeInfo(key, properties);
            var type = typeInfo.AsType();
            return type;
        }


        /// <summary>
        /// from https://stackoverflow.com/a/41785168
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static TypeInfo CompileResultTypeInfo(string typeName, PropertyInfo[] properties) {
            TypeBuilder tb = GetTypeBuilder(typeName);
            ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            var fieldDescriptors = properties.Select(p =>
                new FieldDescriptor(p.Name, p.PropertyType)).ToList();

            var fieldList = fieldDescriptors;

            foreach (var field in fieldList)
                CreateProperty(tb, field.FieldName, field.FieldType);

            TypeInfo objectTypeInfo = tb.CreateTypeInfo();
            return objectTypeInfo;

        }


        /// <summary>
        /// from https://stackoverflow.com/a/41785168
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private static TypeBuilder GetTypeBuilder(string typeName) {
            var typeSignature = typeName;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                   null);
            return tb;

        }


        /// <summary>
        /// from https://stackoverflow.com/a/41785168
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyType"></param>
        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType) {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { propertyType });


            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }

        /// <summary>
        /// Used to populate an entity with property values from
        /// a projection (source)
        /// </summary>
        /// <param name="source">a projection -- an object with a subset of TEntity properties</param>
        /// <param name="destination">the object to which the source data will be copied </param>
        public static void Populate(dynamic source, TEntity destination) {
            var type = source.GetType();
            if (type == typeof(JsonElement))
                throw new NotSupportedException("source is a JsonElement, which is not supported.");
            var dynProps = type.GetProperties();
            foreach (var dynProp in dynProps)
                if (Projection<TEntity>.Properties.TryGetValue(dynProp.Name, out PropertyInfo prop)) {
                    prop.SetValue(destination, dynProp.GetValue(source));
                }
        }

    }


    public class FieldDescriptor {
        public FieldDescriptor(string fieldName, Type fieldType) {
            FieldName = fieldName;
            FieldType = fieldType;
        }
        public string FieldName { get; }
        public Type FieldType { get; }
    }



}

