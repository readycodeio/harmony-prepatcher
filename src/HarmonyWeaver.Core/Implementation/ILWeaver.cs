using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Models;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Default implementation of IILWeaver using Mono.Cecil
    /// </summary>
    public class ILWeaver : IILWeaver
    {
        public void ApplyPatches(AssemblyDefinition targetAssembly, IEnumerable<PatchInfo> patches)
        {
            if (targetAssembly == null)
                throw new ArgumentNullException(nameof(targetAssembly));
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));

            foreach (var patch in patches)
            {
                ApplyPatch(patch.TargetMethod, patch);
            }
        }

        public void ApplyPatch(MethodDefinition targetMethod, PatchInfo patch)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));
            if (patch == null)
                throw new ArgumentNullException(nameof(patch));

            try
            {
                // Apply patches in the correct order
                if (patch.Prefix != null)
                {
                    WeavePrefix(targetMethod, patch.Prefix);
                }

                if (patch.Postfix != null)
                {
                    WeavePostfix(targetMethod, patch.Postfix);
                }

                if (patch.Finalizer != null)
                {
                    WeaveFinalizer(targetMethod, patch.Finalizer);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply patch to method {targetMethod.FullName}: {ex.Message}", ex);
            }
        }

        public void WeavePrefix(MethodDefinition targetMethod, PatchMethodInfo prefixInfo)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));
            if (prefixInfo == null)
                throw new ArgumentNullException(nameof(prefixInfo));

            var il = targetMethod.Body.GetILProcessor();
            var firstInstruction = targetMethod.Body.Instructions[0];

            // Create a method reference to the prefix method
            var prefixMethodRef = targetMethod.Module.ImportReference(prefixInfo.Method);

            // Analyze prefix method parameters to handle special cases like __result
            var prefixParams = prefixInfo.Method.Parameters;
            var hasResultParam = false;
            VariableDefinition? resultVariable = null;

            // Check if prefix has __result parameter (ref parameter)
            foreach (var param in prefixParams)
            {
                if (param.Name == "__result" && param.ParameterType.IsByReference)
                {
                    hasResultParam = true;
                    // Create a local variable to hold the result
                    resultVariable = new VariableDefinition(param.ParameterType.GetElementType());
                    targetMethod.Body.Variables.Add(resultVariable);
                    // Initialize the result variable with default value
                    if (param.ParameterType.GetElementType().Name == "Int32")
                    {
                        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4_0));
                        il.InsertBefore(firstInstruction, il.Create(OpCodes.Stloc, resultVariable));
                    }
                    break;
                }
            }

            // Load arguments for the prefix method
            // We need to map prefix parameters to target method parameters correctly
            int targetParamIndex = 0;
            
            foreach (var param in prefixParams)
            {
                if (param.Name == "__result" && param.ParameterType.IsByReference)
                {
                    // Load address of result variable
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloca, resultVariable));
                }
                else if (param.Name == "__instance")
                {
                    // Load 'this' for instance methods, skip for static methods
                    if (!targetMethod.IsStatic)
                    {
                        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
                    }
                }
                else
                {
                    // Regular parameter - map to target method parameters by position
                    // Skip special parameters when mapping
                    if (targetParamIndex < targetMethod.Parameters.Count)
                    {
                        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg, targetParamIndex + (targetMethod.IsStatic ? 0 : 1)));
                        targetParamIndex++;
                    }
                }
            }

            // Call the prefix method
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, prefixMethodRef));

            // If prefix returns bool, handle skip logic
            if (prefixInfo.Method.ReturnType.Name == "Boolean")
            {
                // Create a label for continuing with original method
                var continueLabel = il.Create(OpCodes.Nop);
                
                // If prefix returned true, continue; if false, return early
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Brtrue, continueLabel));
                
                // Early return logic
                if (targetMethod.ReturnType.Name != "Void")
                {
                    if (hasResultParam && resultVariable != null)
                    {
                        // Return the value set by the prefix method
                        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloc, resultVariable));
                    }
                    else
                    {
                        // Load default value based on return type
                        if (targetMethod.ReturnType.Name == "Int32")
                        {
                            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4_0));
                        }
                        else if (targetMethod.ReturnType.Name == "Boolean")
                        {
                            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4_0));
                        }
                        else if (targetMethod.ReturnType.Name == "Double")
                        {
                            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_R8, 0.0));
                        }
                        else
                        {
                            // For reference types, load null
                            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
                        }
                    }
                }
                
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ret));
                il.InsertBefore(firstInstruction, continueLabel);
            }
        }

        public void WeavePostfix(MethodDefinition targetMethod, PatchMethodInfo postfixInfo)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));
            if (postfixInfo == null)
                throw new ArgumentNullException(nameof(postfixInfo));

            var il = targetMethod.Body.GetILProcessor();
            var postfixMethodRef = targetMethod.Module.ImportReference(postfixInfo.Method);

            // Create a single local variable for the result (shared across all return points)
            VariableDefinition? resultLocal = null;
            if (targetMethod.ReturnType.Name != "Void")
            {
                resultLocal = new VariableDefinition(targetMethod.ReturnType);
                targetMethod.Body.Variables.Add(resultLocal);
            }

            // Find all return instructions and insert postfix calls before them
            var returnInstructions = targetMethod.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ret)
                .ToList();

            foreach (var retInstruction in returnInstructions)
            {
                // For methods with return values, we need to handle the result
                if (targetMethod.ReturnType.Name != "Void" && resultLocal != null)
                {
                    // Store the return value that's currently on the stack
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Stloc, resultLocal));
                    
                    // Load arguments for postfix (original parameters + result)
                    for (int i = 0; i < targetMethod.Parameters.Count; i++)
                    {
                        il.InsertBefore(retInstruction, il.Create(OpCodes.Ldarg, i + (targetMethod.IsStatic ? 0 : 1)));
                    }
                    
                    // Load the result for __result parameter
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloc, resultLocal));
                    
                    // Call postfix (this consumes all arguments from stack)
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Call, postfixMethodRef));
                    
                    // Reload the return value for the ret instruction
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloc, resultLocal));
                }
                else
                {
                    // For void methods, just call postfix with original parameters
                    for (int i = 0; i < targetMethod.Parameters.Count; i++)
                    {
                        il.InsertBefore(retInstruction, il.Create(OpCodes.Ldarg, i + (targetMethod.IsStatic ? 0 : 1)));
                    }
                    
                    // Call postfix (this consumes all arguments from stack)
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Call, postfixMethodRef));
                }
            }
        }

        public void WeaveFinalizer(MethodDefinition targetMethod, PatchMethodInfo finalizerInfo)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));
            if (finalizerInfo == null)
                throw new ArgumentNullException(nameof(finalizerInfo));

            // TODO: Implement proper finalizer weaving with try-catch-finally
            // For now, just skip finalizer patches to allow basic prefix/postfix testing
            // This will be implemented in a future iteration
        }
    }
}