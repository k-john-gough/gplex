using System.IO;
#if !NET20
using System.Reflection;
#endif

namespace QUT.Gplex.IncludeResources
{
    internal class Content
    {
        
        internal static string GplexBuffers
        {
            get
            {
                return GetResourceString("GplexBuffers.txt");
            }
        }

        internal static string GplexxFrame
        {
            get
            {
                return GetResourceString("gplexx.frame");
            }
        }
        
        internal static string ResourceHeader
        {
            get
            {
                return GetResourceString("ResourceHeader.txt");
            }
        }

        static string GetResourceString(string resourceName)
        {
#if NET20
            var assembly = typeof(Content).Assembly;
#else
            var assembly = typeof(Content).GetTypeInfo().Assembly;
#endif
            using (var stream = assembly.GetManifestResourceStream("QUT.Gplex.SpecFiles." + resourceName))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
