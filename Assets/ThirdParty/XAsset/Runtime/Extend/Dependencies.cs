namespace VEngine
{
    public partial class Dependencies : Loadable
    {
        public static void Report(string key, ref string[] dependencies)
        {
            if (Cache.ContainsKey(key))
            {
                var dps = Cache[key];
                if(null != dps)
                {
                    dependencies = new string[dps.bundles.Count];
                    for (int i = 0; i < dps.bundles.Count; ++i)
                    {
                        var bundle = dps.bundles[i];
                        if (null != bundle && null != bundle.assetBundle)
                        {
                            dependencies[i] = bundle.assetBundle.name;
                        }
                    }
                }
            }
        }
    }
}