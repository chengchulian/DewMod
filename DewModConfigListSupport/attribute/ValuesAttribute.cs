using System;

namespace DewModConfigListSupport.attribute;
[AttributeUsage(AttributeTargets.Field)]
public class ValuesAttribute : Attribute
{
    public readonly Type providerType;
    public readonly string methodName;

    public ValuesAttribute(Type providerType, string methodName)
    {
        this.providerType = providerType;
        this.methodName = methodName;
    }
}


