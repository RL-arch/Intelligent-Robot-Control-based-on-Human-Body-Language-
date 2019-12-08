using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.IO;
using System.Threading;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            SerialPort sport = new SerialPort("COM9", 57600, Parity.None, 8, StopBits.One);

            try
            {

                if (sport.IsOpen)
                {
                    sport.Close();
                    // sport.Open(); //open com  
                }
                else
                {
                    sport.Open();//open com  
                }

                String data;
                string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase); //得到当前路径  

                path = path + @"/newfile.txt";
                FileStream TextFile = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);//创建文件  
                byte[] Info;

                while (true)
                {
                    if (sport.BytesToRead != 0)
                    {
                        data = sport.ReadExisting().ToString();//读取串口数据  
                        //this.BeginInvoke(dfun, new object[] { data });  
                        data += "/r/n";
                        Info = new UTF8Encoding(true).GetBytes(data);//转换成字节流  

                        TextFile.Write(Info, 0, Info.Length);//写入文件  
                        Thread.Sleep(10);
                    }
                    else
                    {
                        //this.BeginInvoke(dfun, new object[] { "Serialrev:NULL" });  
                        Thread.Sleep(50);
                    }
                }
            }
            catch (SystemException e)
            {
                // this.BeginInvoke(dfun, new object[] { e.ToString() });  
            }

        }
    }
}