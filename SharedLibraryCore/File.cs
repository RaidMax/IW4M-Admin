using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Http;

namespace SharedLibraryCore
{
    public class RemoteFile : IFile
    {
        string Location;
        string[] FileCache = new string[0];

        public RemoteFile(string location) : base(string.Empty)
        {
            Location = location;
        }

        private void Retrieve()
        {
            using (var cl = new HttpClient())
                FileCache = cl.GetStringAsync(Location).Result.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        public override string[] Tail(int lineCount)
        {
           // Retrieve();
            return FileCache;
        }

        public override long Length()
        {
            Retrieve();
            return FileCache[0].Length;
        }

    }

    public class IFile
    {
        public IFile(String fileName)
        {
            if (fileName != string.Empty)
            {
                Name = fileName;
                Handle = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                sze = Handle.BaseStream.Length;
            }
        }

        public virtual long Length()
        {
            sze = Handle.BaseStream.Length;
            return sze;
        }

        public void Close()
        {
            Handle?.Close();
        }

        public String[] ReadAllLines()
        {
            return Handle?.ReadToEnd().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public String GetText()
        {
            return Handle?.ReadToEnd();
        }

        public virtual String[] Tail(int lineCount)
        {
            var buffer = new List<string>(lineCount);
            string line;
            for (int i = 0; i < lineCount; i++)
            {
                line = Handle.ReadLine();
                if (line == null) return buffer.ToArray();
                buffer.Add(line);
            }

            int lastLine = lineCount - 1;           //The index of the last line read from the buffer.  Everything > this index was read earlier than everything <= this indes

            while (null != (line = Handle.ReadLine()))
            {
                lastLine++;
                if (lastLine == lineCount) lastLine = 0;
                buffer[lastLine] = line;
            }

            if (lastLine == lineCount - 1) return buffer.ToArray();
            var retVal = new string[lineCount];
            buffer.CopyTo(lastLine + 1, retVal, 0, lineCount - lastLine - 1);
            buffer.CopyTo(0, retVal, lineCount - lastLine - 1, lastLine + 1);
            return retVal;
        }
        //END

        private long sze;
        private String Name;
        private StreamReader Handle;
    }
}
