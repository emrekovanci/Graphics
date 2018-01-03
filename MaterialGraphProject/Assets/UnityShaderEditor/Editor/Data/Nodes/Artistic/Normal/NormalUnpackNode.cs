using System.Reflection;
using UnityEngine;
using UnityEditor.Graphing;


namespace UnityEditor.ShaderGraph
{
    [Title("Artistic", "Normal", "Normal Unpack")]
    internal class NormalUnpackNode : CodeFunctionNode
    {
        public NormalUnpackNode()
        {
            name = "Normal Unpack";
        }

        protected override MethodInfo GetFunctionToConvert()
        {
            return GetType().GetMethod("Unity_NormalUnpack", BindingFlags.Static | BindingFlags.NonPublic);
        }

        static string Unity_NormalUnpack(
            [Slot(0, Binding.None)] Vector4 In,
            [Slot(1, Binding.None)] out Vector3 Out)
        {
            Out = Vector3.up;
            return
                @"
{
    Out = UnpackNormalmapRGorAG(In);
}
";
        }
    }
}