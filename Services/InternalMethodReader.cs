using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace MusicBeePlugin.Services
{
    // Metadata inspection only: discovery never calls candidates to determine what they do.
    internal static class InternalMethodReader
    {
        private static readonly Dictionary<ushort, OpCode> codes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)).GroupBy(op => unchecked((ushort)op.Value))
            .ToDictionary(g => g.Key, g => g.First());

        private static IEnumerable<Tuple<OpCode, int>> Operands(MethodInfo method)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null) yield break;
            for (int pos = 0; pos < il.Length;)
            {
                ushort value = il[pos++];
                if (value == 0xfe) { if (pos >= il.Length) yield break; value = (ushort)(0xfe00 | il[pos++]); }
                if (!codes.TryGetValue(value, out var op)) yield break;
                int size;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch:
                        if (pos + 4 > il.Length) yield break;
                        int count = BitConverter.ToInt32(il, pos);
                        if (count < 0 || count > (il.Length - pos - 4) / 4) yield break;
                        size = 4 + count * 4; break;
                    default: size = 4; break;
                }
                if (pos + size > il.Length) yield break;
                if (size == 4) yield return Tuple.Create(op, BitConverter.ToInt32(il, pos));
                pos += size;
            }
        }

        public static IEnumerable<MethodInfo> Calls(MethodInfo method)
        {
            foreach (var operand in Operands(method))
            {
                if (operand.Item1 != OpCodes.Call && operand.Item1 != OpCodes.Callvirt) continue;
                MethodInfo target;
                try { target = method.Module.ResolveMethod(operand.Item2) as MethodInfo; }
                catch (ArgumentException) { continue; }
                if (target != null) yield return target;
            }
        }

        public static bool HasString(MethodInfo method, string value)
        {
            foreach (var operand in Operands(method))
                if (operand.Item1 == OpCodes.Ldstr && method.Module.ResolveString(operand.Item2) == value) return true;
            return false;
        }
    }
}
