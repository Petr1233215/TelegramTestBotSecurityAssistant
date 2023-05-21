using System;

namespace TelegramTestBot
{
    class Program
    {
        static void Main(string[] args)
        {
            //NEED WRITE TELEGRAM BOT CODE
            var bot = new Bot("");
            bot.Start();

            Console.ReadLine();
            bot.Dispose();
        }
    }
}
