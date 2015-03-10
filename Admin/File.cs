using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace IW4MAdmin
{
    class file
    {
        public file(String fileName)
        {
            //Not safe for directories with more than one folder but meh
            _Directory = fileName.Split('\\')[0];
            Name = (fileName.Split('\\'))[fileName.Split('\\').Length-1];

            if (!Directory.Exists(_Directory))
                Directory.CreateDirectory(_Directory);
            
            if (!File.Exists(fileName))
            { 
                FileStream penis = File.Create(fileName);
                penis.Close();
            }
            Handle = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            sze = Handle.BaseStream.Length;
        }

        public file(String file, bool write)
        {
            Name = file;
            writeHandle = new StreamWriter(new FileStream(Name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite));
            sze = 0;
        }

        public long getSize()
        {
            sze = Handle.BaseStream.Length;
            return sze;
        }

        public void Write(String line)
        {
            writeHandle.WriteLine(line);
            writeHandle.Flush();
        }

        public String[] getParameters(int num)
        {
            if (sze > 0)
            {
                String firstLine = Handle.ReadLine();
                String[] Parms = firstLine.Split(':');
                if (Parms.Length < num)
                    return null;
                else
                    return Parms;
            }

            return null;
        }

        public int getNumLines()
        {
            return 0;
        }

        public void Close()
        {
            if(Handle != null)
                Handle.Close();
            if (writeHandle != null)
                writeHandle.Close();
        }

        //FROM http://stackoverflow.com/questions/398378/get-last-10-lines-of-very-large-text-file-10gb-c-sharp
        public string ReadEndTokens()
        {
            Encoding encoding = Encoding.ASCII;
            string tokenSeparator = "\n";
            int numberOfTokens = 2;

            int sizeOfChar = encoding.GetByteCount("\n");
            byte[] buffer = encoding.GetBytes(tokenSeparator);

            using (FileStream fs = new FileStream(this.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Int64 tokenCount = 0;
                Int64 endPosition = fs.Length / sizeOfChar;

                for (Int64 position = sizeOfChar; position < endPosition; position += sizeOfChar)
                {
                    fs.Seek(-position, SeekOrigin.End);
                    fs.Read(buffer, 0, buffer.Length);

                    if (encoding.GetString(buffer) == tokenSeparator)
                    {
                        tokenCount++;
                        if (tokenCount == numberOfTokens)
                        {
                            byte[] returnBuffer = new byte[fs.Length - fs.Position];
                            fs.Read(returnBuffer, 0, returnBuffer.Length);
                            return encoding.GetString(returnBuffer);
                        }
                    }
                }

                // handle case where number of tokens in file is less than numberOfTokens
                fs.Seek(0, SeekOrigin.Begin);
                buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);
                return encoding.GetString(buffer);
            }
        }
        public String[] readAll()
        {
            return Handle.ReadToEnd().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public String[] end(int neededLines)
        {
            var lines = new List<String>();
            while (!Handle.EndOfStream)
            {
                String lins = Handle.ReadLine();
                lines.Add(lins.ToString());
                if (lines.Count > neededLines)
                {
                    lines.RemoveAt(0);
                }
            }

            return lines.ToArray();
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
        StreamReader Handle;
        StreamWriter writeHandle;
    }
}
