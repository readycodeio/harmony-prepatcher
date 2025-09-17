using System.Diagnostics;
using Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

internal class TypeReferenceComparer : IEqualityComparer<TypeReference>
{
    private readonly MethodReferenceComparer _methodComparer;
    private readonly bool _skipGenericOwnerCheck = false;

    public TypeReferenceComparer(bool skipGenericOwnerCheck = false)
    {
        _skipGenericOwnerCheck = skipGenericOwnerCheck;
        if (!skipGenericOwnerCheck)
        {
            _methodComparer = new MethodReferenceComparer();
        }
    }

    public bool Equals(TypeReference x, TypeReference y)
    {
        Debug.Assert(x != null);
        Debug.Assert(y != null);

        if (ReferenceEquals(x, y))
            return true;

        if (x.IsGenericInstance != y.IsGenericInstance ||
            x.IsGenericParameter != y.IsGenericParameter ||
            x.IsFunctionPointer != y.IsFunctionPointer ||
            x.IsArray != y.IsArray ||
            x.IsByReference != y.IsByReference ||
            x.IsPointer != y.IsPointer ||
            x.IsRequiredModifier != y.IsRequiredModifier ||
            x.IsOptionalModifier != y.IsOptionalModifier ||
            x.IsPinned != y.IsPinned)
            return false;

        if (x.IsFunctionPointer)
        {
            var xFunc = x as FunctionPointerType;
            var yFunc = y as FunctionPointerType;
            Debug.Assert(xFunc != null);
            Debug.Assert(yFunc != null);

            if (!Equals(xFunc.ReturnType, yFunc.ReturnType))
                return false;
            if (xFunc.HasParameters != yFunc.HasParameters)
                return false;
            if (xFunc.HasParameters)
            {
                for (var i = 0; i < xFunc.Parameters.Count; i++)
                {
                    var xParam = xFunc.Parameters[i];
                    var yParam = yFunc.Parameters[i];
                    if (!Equals(xParam, yParam))
                        return false;
                }
            }

            return true;
        }

        if (x.IsArray)
        {
            var xArr = x as ArrayType;
            var yArr = y as ArrayType;
            Debug.Assert(xArr != null);
            Debug.Assert(yArr != null);

            if (xArr.Rank != yArr.Rank)
                return false;
            if (!Equals(xArr.ElementType, yArr.ElementType))
                return false;
            if (xArr.Dimensions.Count != yArr.Dimensions.Count)
                return false;
            for (var i = 0; i < xArr.Dimensions.Count; i++)
            {
                var xDim = xArr.Dimensions[i];
                var yDim = yArr.Dimensions[i];
                if (xDim.IsSized != yDim.IsSized ||
                    xDim.LowerBound != yDim.LowerBound ||
                    xDim.UpperBound != yDim.UpperBound)
                    return false;
            }
            return true;
        }

        if (x.IsByReference)
        {
            var xRef = x as ByReferenceType;
            var yRef = y as ByReferenceType;
            Debug.Assert(xRef != null);
            Debug.Assert(yRef != null);

            if (!Equals(xRef.ElementType, yRef.ElementType))
                return false;

            return true;
        }

        if (x.IsPointer)
        {
            var xPtr = x as PointerType;
            var yPtr = y as PointerType;
            Debug.Assert(xPtr != null);
            Debug.Assert(yPtr != null);

            if (!Equals(xPtr.ElementType, yPtr.ElementType))
                return false;

            return true;
        }

        if (x.IsRequiredModifier)
        {
            var xReq = x as RequiredModifierType;
            var yReq = y as RequiredModifierType;
            Debug.Assert(xReq != null);
            Debug.Assert(yReq != null);

            if (!Equals(xReq.ModifierType, yReq.ModifierType))
                return false;
            if (!Equals(xReq.ElementType, yReq.ElementType))
                return false;

            return true;
        }

        if (x.IsOptionalModifier)
        {
            var xReq = x as OptionalModifierType;
            var yReq = y as OptionalModifierType;
            Debug.Assert(xReq != null);
            Debug.Assert(yReq != null);

            if (!Equals(xReq.ModifierType, yReq.ModifierType))
                return false;
            if (!Equals(xReq.ElementType, yReq.ElementType))
                return false;

            return true;
        }

        if (x.IsPinned)
        {
            var xPin = x as PinnedType;
            var yPin = y as PinnedType;
            Debug.Assert(xPin != null);
            Debug.Assert(yPin != null);

            if (!Equals(xPin.ElementType, yPin.ElementType))
                return false;

            return true;
        }

        if (x.IsGenericParameter)
        {
            var xParam = x as GenericParameter;
            var yParam = y as GenericParameter;
            Debug.Assert(xParam != null);
            Debug.Assert(yParam != null);

            if (xParam.Position != yParam.Position)
                return false;

            if (xParam.Type != yParam.Type)
                return false;

            if (!_skipGenericOwnerCheck)
            {
                if (xParam.Type == GenericParameterType.Type)
                    return Equals(
                        xParam.DeclaringType.GetElementType(),
                        yParam.DeclaringType.GetElementType()
                    );
                else
                {
                    var xMt = xParam.DeclaringMethod.GetElementMethod();
                    xMt = xMt.ReplaceDeclaringType(xMt.DeclaringType.GetElementType());
                    var yMt = yParam.DeclaringMethod.GetElementMethod();
                    yMt = yMt.ReplaceDeclaringType(yMt.DeclaringType.GetElementType());

                    return _methodComparer.Equals(xMt, yMt);
                }
            }

            return true;
        }

        if (x.IsGenericInstance)
        {
            var xInst = x as GenericInstanceType;
            var yInst = y as GenericInstanceType;
            Debug.Assert(xInst != null);
            Debug.Assert(yInst != null);

            if (xInst.GenericArguments.Count != yInst.GenericArguments.Count)
                return false;

            for (var i = 0; i < xInst.GenericArguments.Count; i++)
            {
                var xArg = xInst.GenericArguments[i];
                var yArg = yInst.GenericArguments[i];
                if (!Equals(xArg, yArg))
                    return false;
            }
        }

        if (x.IsNested != y.IsNested)
            return false;

        if (x.IsNested)
        {
            if (!Equals(x.DeclaringType, y.DeclaringType))
                return false;
        }

        return x.Name == y.Name && x.Namespace == y.Namespace;
    }

    public int GetHashCode(TypeReference x)
    {
        return x is GenericParameter xParam
            ? xParam.Position * 7
            : x.Name.GetHashCode() * 19 + x.Namespace.GetHashCode() * 17 + 7;
    }
}
