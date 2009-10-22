
using System;
using System.IO;
using System.Xml;
using System.Resources;

namespace ResourceGenerator
{
    public class Program
    {
        public static void Main()
        {
            System.Resources.ResXResourceWriter resourceWriter = new ResXResourceWriter("Content.resx");
            FileStream contentFile;
            StreamReader fileReader;

            contentFile = new System.IO.FileStream("ResourceHeader.txt", FileMode.Open, FileAccess.Read, FileShare.Read);
            fileReader = new StreamReader(contentFile);
            resourceWriter.AddResource("ResourceHeader", fileReader.ReadToEnd());

            contentFile = new System.IO.FileStream("gplexx.frame", FileMode.Open, FileAccess.Read, FileShare.Read);
            fileReader = new StreamReader(contentFile);
            resourceWriter.AddResource("GplexxFrame", fileReader.ReadToEnd());

            contentFile = new System.IO.FileStream("GplexBuffers.txt", FileMode.Open, FileAccess.Read, FileShare.Read);
            fileReader = new StreamReader(contentFile);
            resourceWriter.AddResource("GplexBuffers", fileReader.ReadToEnd());

            resourceWriter.Generate();
            resourceWriter.Close();
        }
    }
}
