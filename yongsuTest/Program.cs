using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;


namespace yongsuTest
{
    
    class Program
    {
        static SerialPrinter printer;
        static String testJson = $@"{{
  ""totalPrice"": 10800,
  ""menus"": [
    {{
      ""isTakeOut"": true,
      ""isTumbler"": false,
      ""name"": ""헤어즐넛라떼"",
      ""options"": [
        {{
          ""name"": ""샷 추가"",
          ""price"": 500,
          ""quantity"": 1
        }},
        {{
          ""name"": ""설탕시럽"",
          ""price"": 500,
          ""quantity"": 0
        }},
        {{
          ""name"": ""헤이즐넛시럽"",
          ""price"": 500,
          ""quantity"": 1
        }},
        {{
          ""name"": ""연하게"",
          ""price"": 0,
          ""quantity"": 0
        }}
      ],
      ""price"": 3200,
      ""temp"": ""아이스"",
      ""totalPrice"": 4200
    }},
    {{
      ""isTakeOut"": false,
      ""isTumbler"": false,
      ""name"": ""카페라떼"",
      ""options"": [
        {{
          ""name"": ""샷 추가"",
          ""price"": 500,
          ""quantity"": 0
        }},
        {{
          ""name"": ""설탕시럽"",
          ""price"": 500,
          ""quantity"": 1
        }},
        {{
          ""name"": ""헤이즐넛시럽"",
          ""price"": 500,
          ""quantity"": 0
        }},
        {{
          ""name"": ""연하게"",
          ""price"": 0,
          ""quantity"": 0
        }}
      ],
      ""price"": 2800,
      ""temp"": ""핫"",
      ""totalPrice"": 3300
    }},
    {{
      ""isTakeOut"": true,
      ""isTumbler"": true,
      ""name"": ""애플주스 스파클링"",
      ""options"": [],
      ""price"": 3500,
      ""temp"": ""아이스"",
      ""totalPrice"": 3300
    }}
  ]
}}";
        static void Main(string[] args)
        {
            string data = null;

            /*//소켓*/
            const int bindPort = 9191;
            TcpListener server = null;
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);
            //포트는 9191로 통일
            try
            {
                IPEndPoint localAddress = new IPEndPoint(IPAddress.Parse("192.168.0.15"), bindPort);
                server = new TcpListener(localAddress);
                server.Start();
                Console.WriteLine("Printer Server Started");

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("연결된 클라이언트 : ", ((IPEndPoint)client.Client.RemoteEndPoint).ToString());
                    NetworkStream stream = client.GetStream();

                    int length;
                    
                    byte[] bytes = new byte[1000];

                    while((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        data = Encoding.Default.GetString(bytes, 0, length);
                        Console.WriteLine("수신됨 : ", data);
                    }
                    // Json 다루는건 https://lovemewithoutall.github.io/it/json-dot-net/ 참고
                    //JObject json = JObject.Parse(testJson);
                    JObject json = null;
                    if (data != null)
                    {
                        json = JObject.Parse(data);
                        PrintTest(json);
                    }
                    //프린터 하고 내용 초기화.
                    data = null; json = null;
                }
            }
            catch(SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        // 파라미터 필요
        static void PrintTest(JObject data)
        {   
            //필요 인수
            int orderNum;
            


            var e = new CustomEpson();
            printer.Write(
              ByteSplicer.Combine(
                e.CenterAlign(),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("주문번호: 1111"),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("--------------------------"),
                e.PrintLine("2021/02/18 15:13:04"),
                e.PrintLine("--------------------------"),

                e.FeedLines(1),
                
                // 메뉴 및 옵션 수에 따라 Loop 돌면서 해야함
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("HOT 에스프레소 (포장)"),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                e.PrintLine("텀블러"),
                e.PrintLine("샷 추가: 1"),
                e.PrintLine("사이즈업: 1"),

                e.FeedLines(1),

                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("ICE 아메리카노 (테이블)"),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                e.PrintLine("텀블러"),
                e.PrintLine("샷 추가: 1"),
                e.PrintLine("사이즈업: 1"),

                e.FeedLines(1),

                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("ICE 아메리카노 (테이블)"),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                e.PrintLine("연하게"),
                e.PrintLine("사이즈업: 1"),

                // e.SetStyles(PrintStyle.None),
                e.FullCut()
              )
            );
        }
    }

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
