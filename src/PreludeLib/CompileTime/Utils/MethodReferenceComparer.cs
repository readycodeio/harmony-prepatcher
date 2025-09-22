extern alias OfficialCecil;
using System.Diagnostics;
using OfficialCecil::Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

internal class MethodReferenceComparer : IEqualityComparer<MethodReference>
{
    private readonly TypeReferenceComparer _typeComparer;
    
    public MethodReferenceComparer()
    {
        _typeComparer = new TypeReferenceComparer(true);
    }

    public bool Equals(MethodReference x, MethodReference y)
    {
        Debug.Assert(x != null);
        Debug.Assert(y != null);
        
        if (ReferenceEquals(x, y))
            return true;

        if (x.Name != y.Name || !_typeComparer.Equals(x.DeclaringType, y.DeclaringType) ||
            x.IsGenericInstance != y.IsGenericInstance ||
            x.HasParameters != y.HasParameters || x.HasThis != y.HasThis ||
            x.ExplicitThis != y.ExplicitThis ||
            x.Parameters.Count != y.Parameters.Count)
            return false;

        if (!_typeComparer.Equals(x.ReturnType, y.ReturnType))
            return false;
        
        for (var i = 0; i < x.Parameters.Count; i++)
        {
            var xParam = x.Parameters[i];
            var yParam = y.Parameters[i];
            if (!_typeComparer.Equals(xParam.ParameterType, yParam.ParameterType))
                return false;
        }
        
        if (x.IsGenericInstance)
        {
            var xInst = x as GenericInstanceMethod;
            var yInst = y as GenericInstanceMethod;
            Debug.Assert(xInst != null);
            Debug.Assert(yInst != null);

            if (xInst.GenericArguments.Count != yInst.GenericArguments.Count)
                return false;

            for (var i = 0; i < xInst.GenericArguments.Count; i++)
            {
                var xArg = xInst.GenericArguments[i];
                var yArg = yInst.GenericArguments[i];

                if (!_typeComparer.Equals(xArg, yArg))
                    return false;
            }
        }

        return true;
    }

    public int GetHashCode(MethodReference x)
    {
        return x.Name.GetHashCode() * 13 + _typeComparer.GetHashCode(x.ReturnType) * 11 + 7;
    }
}
