using System.Collections.Generic;

namespace DewModConfigListSupport.config;

public static class ConfigValues
{
    private static readonly List<string> TestValues = ["text1", "text2", "text3"];

    public static IEnumerable<string> GetTestValues()
    {
        return TestValues;
    }
}
