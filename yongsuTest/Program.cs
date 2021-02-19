using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
            Console.WriteLine("※ 창을 닫지 말아주세요. 영업 종료시에만 닫으시면 됩니다. ※");
            TcpServerTest();  
        }

        static void TcpServerTest()
        {
            //github test2
            string bindIp = "127.0.0.1";
            const int bindPort = 9292;
            
            TcpListener server = null;
            try
            {
                IPEndPoint localAddress = new IPEndPoint(IPAddress.Parse(bindIp), bindPort);

                server = new TcpListener(localAddress);

                server.Start();

                Console.WriteLine("프린터 서버 시작...");

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("클라이언트 접속: {0} ", ((IPEndPoint)client.Client.RemoteEndPoint).ToString());

                    NetworkStream stream = client.GetStream();

                    int length;
                    string data = null;
                    byte[] bytes = new byte[10000];

                    while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        data = Encoding.Default.GetString(bytes, 0, length);
                        Console.WriteLine(String.Format("수신: {0}", data));
                       
                        // byte[] msg = Encoding.Default.GetBytes(data);
                        // stream.Write(msg, 0, msg.Length);
                        // Console.WriteLine(String.Format("송신: {0}", data));
                    }
                    JObject json = null;
                    if(data != null)
                    {
                        json = JObject.Parse(data);
                        PrintTest(json);
                    }
                    json = null;
                    data = null;
                    stream.Close();
                    client.Close();
                }

            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                server.Stop();
            }

            Console.WriteLine("서버를 종료합니다.");
        }

        // 파라미터 필요
        static void PrintTest(JObject json)
        {
            //프린터 연결
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);

            // 옵션 없는 메뉴들 수량 체크
            Dictionary<String, int> menuWithoutOptionsCount = new Dictionary<string, int>();

            foreach (var menu in json["menus"])
            {
                var options = menu["options"].ToObject<JArray>();
                if (options.Count > 0) {
                    continue;
                }

                var menu_name = menu["name"].ToString();

                if (menuWithoutOptionsCount.ContainsKey(menu_name))
                {
                    menuWithoutOptionsCount[menu_name] = menuWithoutOptionsCount[menu_name] + 1;
                } else
                {
                    menuWithoutOptionsCount.Add(menu_name, 1);
                }
            }

            string orderNum = json["orderNum"].ToString();
            Console.WriteLine(orderNum);
            var e = new CustomEpson();
            printer.Write(
              ByteSplicer.Combine(
                e.FeedLines(1),
                e.CenterAlign(),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("주문번호: " + orderNum),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("--------------------------"),
                e.PrintLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")),//시간
                e.PrintLine("--------------------------"),

                e.FeedLines(1))
              );

            SortedSet<String> menuWithoutOptionsSet = new SortedSet<string>();
           foreach (var menu in json["menus"])
            {
                string menu_pojang = menu["isTakeOut"].Value<bool>() ? "포장" : "테이블";
                string menu_name = menu["name"].ToString();
                string menu_tumbler = menu["isTumbler"].Value<bool>() ? "텀블러" : null;
                string menu_temp = menu["temp"].ToString(); // 아이스, 핫
                JArray optionsArray = menu["options"].ToObject<JArray>();

                Console.WriteLine("({0}) {1} ({2})", menu_temp, menu_name, menu_pojang);

                if (menu_tumbler != null)
                {
                    Console.WriteLine("    {0}", menu_tumbler);
                }

                String menu_name_line = "(" + menu_temp + ") " + menu_name + " (" + menu_pojang + ")";

                if (optionsArray.Count <= 0 && menuWithoutOptionsSet.Contains(menu_name_line) == false)
                {
                    printer.Write(
                      ByteSplicer.Combine(
                        // 메뉴 및 옵션 수에 따라 Loop 돌면서 해야함
                        e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                        e.PrintLine(menu_name_line + " X " + menuWithoutOptionsCount[menu_name]))
                    );
                    menuWithoutOptionsSet.Add(menu_name_line);
                } else if (optionsArray.Count > 0)
                {
                    printer.Write(
                      ByteSplicer.Combine(
                        // 메뉴 및 옵션 수에 따라 Loop 돌면서 해야함
                        e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                        e.PrintLine(menu_name_line))
                    );
                }

                if (menu_tumbler != null) {
                    printer.Write(
                        ByteSplicer.Combine(
                    e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                    e.PrintLine(menu_tumbler)
                    )
                    );
                }
                
                foreach (var option in optionsArray)
                {
                    string _option = String.Format("    {0}: {1}", option["name"].ToString(), option["quantity"].ToString());
                    Console.WriteLine(_option);
                    if (option["quantity"].ToString().Equals("0"))
                    {
                        continue;
                    }
                    printer.Write(
                    ByteSplicer.Combine(
                        e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                        e.PrintLine(_option)
                    )
                );
                }

                printer.Write(
                    ByteSplicer.Combine(
                    e.FeedLines(1)
                    )
                    // e.SetStyles(PrintStyle.None),
                    );
            }
            printer.Write(
                ByteSplicer.Combine(e.FeedLines(3)));

            printer.Write(
                ByteSplicer.Combine(
                    e.FullCut()
                )
            );
            //프린터 해제
            printer.Dispose();
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
