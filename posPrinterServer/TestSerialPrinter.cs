using System;
using System.Text;

namespace posPrinterServer
{
    class TestSerialPrinter
    {
        public TestSerialPrinter(string portName, int baudRate)
        {

        }

        public void Write(byte[] bytes)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = System.Text.Encoding.GetEncoding("euc-kr");
            Console.WriteLine(encoding.GetString(bytes));
        }

        public void Dispose()
        {

        }
    }
}