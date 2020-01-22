using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;

namespace ExploringDynamicDeserialization {

    //https://stackoverflow.com/a/41785168
    public class AnonymousTypes<TEntity> {


        private static Dictionary<string, PropertyInfo> ClassProperties { get; }

        private static readonly string keyBase;
        private static readonly Dictionary<string, int> propIndex;
        private static ConcurrentDictionary<string, Type> TypeDictionary { get; }

        /// <summary>
        /// Statically initializes the ClassProperties via reflection
        /// </summary>
        static AnonymousTypes() {
            ClassProperties = new Dictionary<string, PropertyInfo>();
            propIndex = new Dictionary<string, int>();
            var props = typeof(TEntity).GetProperties();
            for (int i = 0; i < props.Length;i++) {
                propIndex.Add(props[i].Name.ToLower(),i);
                keyBase += props[i].Name.ToLower()[0];
                ClassProperties.Add(props[i].Name, props[i]);
            }
            TypeDictionary = new ConcurrentDictionary<string, Type>();
        }


        public static Type GetOrAddType(IEnumerable<string> dynamicProps) {
            var key = GetKey(dynamicProps);
            return TypeDictionary.GetOrAdd(key, CreateNewType(key, dynamicProps));
        }

        private static string GetKey(IEnumerable<string> dynamicProps) {
            var ca = keyBase.ToCharArray();
            foreach(var dp in dynamicProps) {
                if (propIndex.TryGetValue(dp.ToLower(), out int idx))
                    ca[idx] = dp.ToUpper()[0];
            }
            return $"{typeof(TEntity).FullName}_{new string(ca)}";
        }

        public static Type CreateNewType(string key, IEnumerable<string> dynamicProps) {
            PropertyInfo[] properties = ClassProperties.Where(p => dynamicProps.Any(f => f.Equals(p.Key,StringComparison.OrdinalIgnoreCase))).Select(f => f.Value).ToArray();
            var myTypeInfo = CompileResultTypeInfo(key, properties);
            var myType = myTypeInfo.AsType();
            return myType;
        }


        public static TypeInfo CompileResultTypeInfo(string typeName, PropertyInfo[] properties) {
            TypeBuilder tb = GetTypeBuilder(typeName);
            ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            var fieldDescriptors = properties.Select(p =>
                new FieldDescriptor(p.Name, p.PropertyType)).ToList();

            var yourListOfFields = fieldDescriptors;

            foreach (var field in yourListOfFields)
                CreateProperty(tb, field.FieldName, field.FieldType);

            TypeInfo objectTypeInfo = tb.CreateTypeInfo();
            return objectTypeInfo;

        }


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

