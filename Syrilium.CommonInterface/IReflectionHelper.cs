using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Syrilium.CommonInterface
{
    public interface IReflectionHelper
    {
        /// <summary>
        /// Instead use Type.IsAssignableFrom
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="derivedType"></param>
        /// <returns></returns>
        [Obsolete("Instead use Type.IsAssignableFrom")]
        bool IsTypeOf(Type requestedType, Type derivedType);
        object CreateNewInstance(Type type, params object[] parametersForConstructor);
        object CreateNewInstance(Type type, Type[] parametersTypes, object[] parametersForConstructor, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo GetMethodOnType(Type type, string methodName, Type[] types = null, BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, bool getBaseMethod = false);
    }
}
