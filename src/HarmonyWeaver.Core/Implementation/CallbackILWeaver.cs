using HarmonyWeaver.Core.Callbacks;
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
    /// IL weaver that injects callback hooks instead of direct method calls
    /// This eliminates dependency cycles between patch and target assemblies
    /// </summary>
    public class CallbackILWeaver : IILWeaver
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
                // Inject callback fields into the target type
                InjectCallbackFields(targetMethod, patch);

                // Modify the method to call the callbacks
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
                throw new InvalidOperationException($"Failed to apply callback patch to method {targetMethod.FullName}: {ex.Message}", ex);
            }
        }

        public void WeavePrefix(MethodDefinition targetMethod, PatchMethodInfo prefixInfo)
        {
            var il = targetMethod.Body.GetILProcessor();
            var firstInstruction = targetMethod.Body.Instructions[0];

            // Create callback field reference
            var callbackFieldName = $"__harmony_prefix_{targetMethod.Name}";
            var callbackField = targetMethod.DeclaringType.Fields.FirstOrDefault(f => f.Name == callbackFieldName);
            
            if (callbackField == null)
                throw new InvalidOperationException($"Callback field {callbackFieldName} not found in {targetMethod.DeclaringType.FullName}");

            // Create labels for control flow
            var continueLabel = il.Create(OpCodes.Nop);
            var skipLabel = il.Create(OpCodes.Nop);

            // Check if callback is null
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldsfld, callbackField));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Brfalse, continueLabel));

            // Create arguments array
            var argsLocal = CreateArgumentsArray(targetMethod, il, firstInstruction);

            // Create result variable for out parameter
            var resultLocal = new VariableDefinition(targetMethod.Module.ImportReference(typeof(object)));
            targetMethod.Body.Variables.Add(resultLocal);
            
            // Initialize result to null
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Stloc, resultLocal));

            // Prepare for callback call: callback, args, out result
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldsfld, callbackField));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloc, argsLocal));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloca, resultLocal));

            // Call the prefix callback
            var callbackMethod = targetMethod.Module.ImportReference(
                typeof(PrefixCallback).GetMethod("Invoke"));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, callbackMethod));

            // Check the result - if false, skip to return custom result
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Brfalse, skipLabel));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Br, continueLabel));

            // Skip block - return the custom result
            il.InsertBefore(firstInstruction, skipLabel);
            
            if (targetMethod.ReturnType.Name != "Void")
            {
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloc, resultLocal));
                
                // Convert result to appropriate type if needed
                if (targetMethod.ReturnType.IsValueType)
                {
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Unbox_Any, targetMethod.ReturnType));
                }
                else if (targetMethod.ReturnType.FullName != "System.Object")
                {
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Castclass, targetMethod.ReturnType));
                }
            }
            
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ret));

            // Continue label
            il.InsertBefore(firstInstruction, continueLabel);
        }

        public void WeavePostfix(MethodDefinition targetMethod, PatchMethodInfo postfixInfo)
        {
            var il = targetMethod.Body.GetILProcessor();

            // Create callback field reference
            var callbackFieldName = $"__harmony_postfix_{targetMethod.Name}";
            var callbackField = targetMethod.DeclaringType.Fields.FirstOrDefault(f => f.Name == callbackFieldName);
            
            if (callbackField == null)
                throw new InvalidOperationException($"Callback field {callbackFieldName} not found in {targetMethod.DeclaringType.FullName}");

            // Find all return instructions and insert postfix calls before them
            var returnInstructions = targetMethod.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ret)
                .ToList();

            // Create shared local variables
            var argsLocal = new VariableDefinition(targetMethod.Module.ImportReference(typeof(object[])));
            var resultLocal = targetMethod.ReturnType.Name != "Void" ? 
                new VariableDefinition(targetMethod.Module.ImportReference(typeof(object))) : null;

            targetMethod.Body.Variables.Add(argsLocal);
            if (resultLocal != null)
                targetMethod.Body.Variables.Add(resultLocal);

            foreach (var retInstruction in returnInstructions)
            {
                var continueLabel = il.Create(OpCodes.Nop);

                // Check if callback is null
                il.InsertBefore(retInstruction, il.Create(OpCodes.Ldsfld, callbackField));
                il.InsertBefore(retInstruction, il.Create(OpCodes.Brfalse, continueLabel));

                // Handle return value if present
                if (targetMethod.ReturnType.Name != "Void" && resultLocal != null)
                {
                    // Box the return value and store it
                    if (targetMethod.ReturnType.IsValueType)
                    {
                        il.InsertBefore(retInstruction, il.Create(OpCodes.Box, targetMethod.ReturnType));
                    }
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Stloc, resultLocal));
                }

                // Create arguments array
                CreateArgumentsArrayAtReturnPoint(targetMethod, il, retInstruction, argsLocal);

                // Call the postfix callback
                il.InsertBefore(retInstruction, il.Create(OpCodes.Ldsfld, callbackField));
                il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloc, argsLocal));
                
                if (resultLocal != null)
                {
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloca, resultLocal));
                }
                else
                {
                    // For void methods, pass a dummy reference
                    var dummyLocal = new VariableDefinition(targetMethod.Module.ImportReference(typeof(object)));
                    targetMethod.Body.Variables.Add(dummyLocal);
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldnull));
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Stloc, dummyLocal));
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloca, dummyLocal));
                }

                var postfixCallbackMethod = targetMethod.Module.ImportReference(
                    typeof(PostfixCallback).GetMethod("Invoke"));
                il.InsertBefore(retInstruction, il.Create(OpCodes.Callvirt, postfixCallbackMethod));

                // Reload the (possibly modified) result
                if (targetMethod.ReturnType.Name != "Void" && resultLocal != null)
                {
                    il.InsertBefore(retInstruction, il.Create(OpCodes.Ldloc, resultLocal));
                    
                    // Unbox/cast back to the original type
                    if (targetMethod.ReturnType.IsValueType)
                    {
                        il.InsertBefore(retInstruction, il.Create(OpCodes.Unbox_Any, targetMethod.ReturnType));
                    }
                    else
                    {
                        il.InsertBefore(retInstruction, il.Create(OpCodes.Castclass, targetMethod.ReturnType));
                    }
                }

                il.InsertBefore(retInstruction, continueLabel);
            }
        }

        public void WeaveFinalizer(MethodDefinition targetMethod, PatchMethodInfo finalizerInfo)
        {
            // TODO: Implement finalizer callback weaving with try-catch-finally
            // For now, skip finalizer patches
        }

        private void InjectCallbackFields(MethodDefinition targetMethod, PatchInfo patch)
        {
            var targetType = targetMethod.DeclaringType;
            var module = targetMethod.Module;

            // Inject callback fields for each patch type
            if (patch.Prefix != null)
            {
                var fieldName = $"__harmony_prefix_{targetMethod.Name}";
                if (!targetType.Fields.Any(f => f.Name == fieldName))
                {
                    var callbackType = module.ImportReference(typeof(PrefixCallback));
                    var field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Public, callbackType);
                    targetType.Fields.Add(field);
                }
            }

            if (patch.Postfix != null)
            {
                var fieldName = $"__harmony_postfix_{targetMethod.Name}";
                if (!targetType.Fields.Any(f => f.Name == fieldName))
                {
                    var callbackType = module.ImportReference(typeof(PostfixCallback));
                    var field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Public, callbackType);
                    targetType.Fields.Add(field);
                }
            }

            if (patch.Finalizer != null)
            {
                var fieldName = $"__harmony_finalizer_{targetMethod.Name}";
                if (!targetType.Fields.Any(f => f.Name == fieldName))
                {
                    var callbackType = module.ImportReference(typeof(FinalizerCallback));
                    var field = new FieldDefinition(fieldName, FieldAttributes.Static | FieldAttributes.Public, callbackType);
                    targetType.Fields.Add(field);
                }
            }
        }

        private VariableDefinition CreateArgumentsArray(MethodDefinition method, ILProcessor il, Instruction insertPoint)
        {
            var argsLocal = new VariableDefinition(method.Module.ImportReference(typeof(object[])));
            method.Body.Variables.Add(argsLocal);

            // Create array with size equal to parameter count
            il.InsertBefore(insertPoint, il.Create(OpCodes.Ldc_I4, method.Parameters.Count));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Newarr, method.Module.ImportReference(typeof(object))));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Stloc, argsLocal));

            // Fill the array with parameters
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldloc, argsLocal));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldc_I4, i));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldarg, i + (method.IsStatic ? 0 : 1)));
                
                // Box value types
                if (method.Parameters[i].ParameterType.IsValueType)
                {
                    il.InsertBefore(insertPoint, il.Create(OpCodes.Box, method.Parameters[i].ParameterType));
                }
                
                il.InsertBefore(insertPoint, il.Create(OpCodes.Stelem_Ref));
            }

            return argsLocal;
        }

        private void CreateArgumentsArrayAtReturnPoint(MethodDefinition method, ILProcessor il, Instruction insertPoint, VariableDefinition argsLocal)
        {
            // Create array with size equal to parameter count
            il.InsertBefore(insertPoint, il.Create(OpCodes.Ldc_I4, method.Parameters.Count));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Newarr, method.Module.ImportReference(typeof(object))));
            il.InsertBefore(insertPoint, il.Create(OpCodes.Stloc, argsLocal));

            // Fill the array with parameters
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldloc, argsLocal));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldc_I4, i));
                il.InsertBefore(insertPoint, il.Create(OpCodes.Ldarg, i + (method.IsStatic ? 0 : 1)));
                
                // Box value types
                if (method.Parameters[i].ParameterType.IsValueType)
                {
                    il.InsertBefore(insertPoint, il.Create(OpCodes.Box, method.Parameters[i].ParameterType));
                }
                
                il.InsertBefore(insertPoint, il.Create(OpCodes.Stelem_Ref));
            }
        }
    }
}