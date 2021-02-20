using System.Text;
using ESCPOS_NET.Emitters;

namespace posPrinterServer
{
    class CustomEpson : EPSON
    {
        public override byte[] PrintLine(string line)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string text = line;
            var encoding = System.Text.Encoding.GetEncoding("euc-kr");
            byte[] bytes = encoding.GetBytes(text);
            string isoString = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
            return base.PrintLine(isoString);
        }
    }
}