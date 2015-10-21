using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WcfService
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ServiceHost host = new ServiceHost(typeof(TicTacService));
                host.Open();
                Console.WriteLine("Сервер запущен. Для завершения нажмите любую кнопку.\n");
                Thread thrChechUserTime = new Thread(TicTacService.CheckUserLastTime);
                thrChechUserTime.IsBackground = true;
                thrChechUserTime.Start();
                Console.ReadKey();
                host.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadKey();
            }
        }
    }
}