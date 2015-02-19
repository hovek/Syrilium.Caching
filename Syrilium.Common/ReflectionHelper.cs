using Syrilium.CommonInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Syrilium.Common
{
    public class ReflectionHelper : IReflectionHelper
    {
        /// <summary>
        /// Instead use Type.IsAssignableFrom
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="derivedType"></param>
        /// <returns></returns>
        [Obsolete("Instead use Type.IsAssignableFrom")]
        public bool IsTypeOf(Type baseType, Type derivedType)
        {
            if (!baseType.IsInterface && !derivedType.IsInterface)
                return isTypeOf(baseType, derivedType);
            else if (baseType.IsInterface && derivedType.IsInterface)
            {
                if (baseType == derivedType) return true;
                foreach (Type t in derivedType.GetInterfaces())
                {
                    if (baseType == t)
                        return true;
                }
                return false;
            }
            else if (baseType.IsInterface)
            {
                foreach (Type t in derivedType.GetInterfaces())
                {
                    if (baseType == t)
                        return true;
                }
                return false;
            }
            else
            {
                Type[] derInterfaces = derivedType.GetInterfaces();
                foreach (Type t in baseType.GetInterfaces())
                {
                    if (derivedType == t || derInterfaces.Contains(t))
                        return true;
                }
                return false;
            }
        }

        private static bool isTypeOf(Type requestedType, Type derivedType)
        {
            if (requestedType == derivedType)
            {
                return true;
            }
            else if (derivedType.BaseType != null)
            {
                return isTypeOf(requestedType, derivedType.BaseType);
            }
            else
            {
                return false;
            }
        }

        public object CreateNewInstance(Type type, params object[] parametersForConstructor)
        {
            Type[] types = new Type[parametersForConstructor.Length];
            for (int i = 0; i < parametersForConstructor.Length; i++)
            {
                types[i] = parametersForConstructor[i].GetType();
            }
            return CreateNewInstance(type, types, parametersForConstructor);
        }

        public object CreateNewInstance(Type type, Type[] parametersTypes, object[] parametersForConstructor, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        {
            return type.GetConstructor(bindingAttr, null, parametersTypes, null).Invoke(parametersForConstructor);
        }

        public MethodInfo GetMethodOnType(Type type, string methodName, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, bool getBaseMethod = false)
        {
            MethodInfo mi;
            if (type.IsInterface)
            {
                var infs = new List<Type> { type };
                infs.AddRange(type.GetInterfaces());
                if (types == null)
                {
                    foreach (var inf in infs)
                    {
                        mi = inf.GetMethod(methodName, bindingAttr);
                        if (mi != null) return mi;
                    }
                }
                else
                {
                    foreach (var inf in infs)
                    {
                        mi = inf.GetMethod(methodName, bindingAttr, null, types, null);
                        if (mi != null) return mi;
                    }
                }
                return null;
            }

            if (types == null)
                mi = type.GetMethod(methodName, bindingAttr);
            else
                mi = type.GetMethod(methodName, bindingAttr, null, types, null);
            if (getBaseMethod && mi != null)
                return mi.GetRuntimeBaseDefinition();
            return mi;
        }
    }
}
