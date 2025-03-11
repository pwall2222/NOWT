using System.Collections.Generic;

static class SafeDict
{
    public static TV GetValue<TK, TV>(
        this IDictionary<TK, TV> dict,
        TK key,
        TV defaultValue = default(TV)
    )
    {
        TV value;
        return dict.TryGetValue(key, out value) ? value : defaultValue;
    }
}
