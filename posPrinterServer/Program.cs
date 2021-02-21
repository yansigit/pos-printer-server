using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace posPrinterServer
{
    class Program
    {
        static SerialPrinter printer;
        static CustomEpson e;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            WriteLineCenter("※ 창을 닫지 말아주세요. 영업 종료시에만 닫으시면 됩니다. ※");
            e = new CustomEpson();
            TestThermalPrinter();
            Console.ForegroundColor = ConsoleColor.White;
            OpenTcpServer();  
        }

        static void WriteLineCenter(string s)
        {
            Console.SetCursorPosition((Console.WindowWidth - s.Length) / 2, Console.CursorTop);
            Console.WriteLine(s);
        }

        static void TestThermalPrinter()
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                WriteLineCenter("프린트 연결 테스트..");
                var test = new SerialPrinter(portName: "COM1", baudRate: 9600);
                test.Dispose();
            }
            catch (Exception e)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                WriteLineCenter("프린트와 연결에 실패했습니다. 프로그램을 종료합니다.");
                Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine(e.ToString());
                Environment.Exit(0);
            }
        }

        static void OpenTcpServer()
        {
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
                    byte[] bytes = new byte[15000];

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
                        StartPrint(json);
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

        // 프린트 함수
        static void StartPrint(JObject json)
        {
            //프린터 연결
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);

            // 메뉴 해쉬코드 - 내용 딕셔너리
            Dictionary<int, JToken> menuDictionary = new Dictionary<int, JToken>();

            // 각 메뉴마다 수량 겟하고 내용 저장
            foreach (var menu in json["menus"])
            {
                int menuHash = menu.ToString().GetHashCode();

                if (menuDictionary.ContainsKey(menuHash))
                    menuDictionary[menuHash]["quantity"] = menuDictionary[menuHash]["quantity"].ToObject<int>() + 1;
                else
                {
                    menu["quantity"] = 1;

                    var option = menu["options"].First;

                    // 옵션 확인하고 수량 없으면 삭제
                    while (option != null)
                    {
                        var currentOption = option;
                        option = currentOption.Next;
                        if (currentOption["quantity"].ToObject<int>() <= 0)
                            currentOption.Remove();
                    }

                    menuDictionary.Add(menuHash, menu);
                }
            }
            
            string orderNum = json["orderNum"].ToString();

            printer.Write(
              ByteSplicer.Combine(
                e.FeedLines(1),
                e.CenterAlign(),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                e.PrintLine("주문번호: " + orderNum),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("--------------------------"),
                e.PrintLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")),//시간
                e.PrintLine("--------------------------")
              ));

            byte[] bodyBytes = e.FeedLines(1);
            foreach (var menu in menuDictionary.Values)
            {
                string menu_pojang = menu["isTakeOut"].Value<bool>() ? "포장" : "테이블";
                string menu_name = menu["name"].ToString();
                string menu_tumbler = menu["isTumbler"].Value<bool>() ? "텀블러" : null;
                string menu_temp = menu["temp"].ToString(); // 아이스, 핫
                int menu_quantity = menu["quantity"].ToObject<int>();

                // (온도) 메뉴이름 (포장여부)
                string menu_name_line = "(" + menu_temp + ") " + menu_name + " (" + menu_pojang + ")";

                byte[] menuBytes = ByteSplicer.Combine(
                    e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                    e.PrintLine(menu_name_line),
                    e.PrintLine("수량 : " + menu_quantity)
                );
                if (menu["options"].Any())
                {
                    menuBytes = ByteSplicer.Combine(menuBytes,
                        e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth)
                        // , e.PrintLine("옵션")
                    );
                    JArray optionsArray = menu["options"].ToObject<JArray>();

                    foreach (var option in optionsArray)
                    {
                        menuBytes = ByteSplicer.Combine(menuBytes,
                            e.PrintLine(option["name"].ToString() + " : " + option["quantity"].ToString())
                        );
                    }
                }

                bodyBytes = ByteSplicer.Combine(bodyBytes, menuBytes, e.FeedLines(1));
            }

            printer.Write(bodyBytes);
            
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
}
