extern alias OfficialCecil;
using System.Diagnostics;
using OfficialCecil::Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

internal class TypeTemplateReferenceComparer : IEqualityComparer<TypeReference>
{
    private readonly TypeReferenceComparer _typeComparer;

    public TypeTemplateReferenceComparer()
    {
        _typeComparer = new TypeReferenceComparer();
    }
    
    public bool Equals(TypeReference x, TypeReference y)
    {
        Debug.Assert(x != null);
        Debug.Assert(y != null);

        if (x.IsGenericInstance)
            x = x.GetElementType();
        if (y.IsGenericInstance)
            y = y.GetElementType();

        if (ReferenceEquals(x, y))
            return true;

        return _typeComparer.Equals(x, y);
    }

    public int GetHashCode(TypeReference x)
    {
        if (x.IsGenericInstance)
            x = x.GetElementType();
        return _typeComparer.GetHashCode(x);
    }
}
