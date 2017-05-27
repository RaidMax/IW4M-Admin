using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace SharedLibrary
{
    public class IFile
    {
        public IFile(String fileName)
        {
            //Not safe for directories with more than one folder but meh
            string[] asd = fileName.Split('/');

            if (asd[0] != "")
                _Directory = asd[0];
            else
                _Directory = asd[2];
      
            Name = (fileName.Split('/'))[fileName.Split('/').Length - 1];


            try
            {
                Handle = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                sze = Handle.BaseStream.Length;
            }

            catch
            {
                //Console.WriteLine("Unable to open log file for writing!");
            }
        }

        public IFile(String file, bool write)
        {
            Name = file;
            writeHandle = new StreamWriter(new FileStream(Name, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
            sze = 0;
        }

        public IFile()
        {
            WebClient request = new WebClient();
            string url = $"http://raidmax.org/logs/IW4X/games_mp.log";
            byte[] newFileData = request.DownloadData(url);

            Handle = new StreamReader(new MemoryStream(newFileData));
            sze = Handle.BaseStream.Length;
        }

        public long getSize()
        {
            sze = Handle.BaseStream.Length;
            return sze;
        }

        public void Write(String line)
        {
            if (writeHandle != null)
            {
                try
                {
                    writeHandle.WriteLine(line);
                    writeHandle.Flush();
                }

                catch (Exception E)
                {
                    Console.WriteLine("Error during flush", E.Message);
                }
            }
        }


        public void Close()
        {
            if (Handle != null)
                Handle.Close();
            if (writeHandle != null)
                writeHandle.Close();
        }

        public String[] readAll()
        {
            return Handle.ReadToEnd().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public String getLines()
        {
            return Handle.ReadToEnd();
        }

        public String[] Tail(int lineCount)
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
        private String _Directory;
        private StreamReader Handle;
        private StreamWriter writeHandle;
    }
}
