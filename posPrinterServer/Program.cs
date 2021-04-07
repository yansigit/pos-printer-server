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
using WMPLib;
using System.Threading;
///////////////////////////////////////////////////
//       영수증 재출력용 JSON                        // 
//                                               //
//{                                              //
//    action : "reprint",                        //
//    totalPrice : 모든 메뉴 총가격,                 //
//    approvalDate : 승인일자,                     //
//    approvalNum : 승인번호,                      //
//    cardName : 카드이름,                         //
//    cardNum : 카드번호,                          //
//    acqName : 매입사명,                          //
//    menus : {                                 //
//              {                               // 
//                  name : 음료명,                //
//                  totalPrice: 옵션을 합한 가격,   //
//                  quantity : 수량              //
//              }                               //
//            },                                //
//            ...                               //
//                                              //
//}                                             //
//////////////////////////////////////////////////



namespace posPrinterServer
{
    class Program
    {
#if DEBUG
        static TestSerialPrinter printer;
#else
        static SerialPrinter printer;
#endif
        static CustomEpson e;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            WriteLineCenter("※ 창을 닫지 말아주세요. 영업 종료시에만 닫으시면 됩니다. ※");
            e = new CustomEpson();
#if !DEBUG
            TestThermalPrinter();
#endif
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

       
        static void PlayOrderSound()
        {
            Console.WriteLine("알람이 울립니다");
            WMPLib.WindowsMediaPlayer player = new WMPLib.WindowsMediaPlayer();
            player.URL = @"order_sound.mp3";
            player.controls.play();
        }

        static void OpenTcpServer()
        {
            string bindIp = "127.0.0.1";
            const int bindPort = 13522;
            
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
                        if (json["action"] == null)
                        {
                            try
                            {
                                var rTh = new Thread(PlayOrderSound);
                                rTh.Start();
                                StartPrint(json);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                Console.WriteLine("프린트가 사용중입니다. 타 앱에서 프린트를 사용 종료 후 다시 실행하세요.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("액션: " + json["action"].ToString());
                            if (json["action"].ToString() == "printJungsan")
                            {
                                try
                                {
                                    StartPrintJungsan(json);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                    Console.WriteLine("프린트가 사용중입니다. 타 앱에서 프린트를 사용 종료 후 다시 실행하세요.");
                                }
                            }
                            //Todo
                            if (json["action"].ToString() == "rePrint")
                            {
                                //여기 구현해 주십시오..
                                try
                                {
                                    reprintBill(json);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                    Console.WriteLine("프린트가 사용중입니다. 타 앱에서 프린트를 사용 종료 후 다시 시도해주세요.");
                                }
                            }
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

        static void StartPrintJungsan(JObject json)
        {
#if DEBUG
            printer = new TestSerialPrinter(portName: "COM1", baudRate: 9600);
#else
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);
#endif

            printer.Write(
                ByteSplicer.Combine(
                    e.CenterAlign(),
                    e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth | PrintStyle.Bold),
                    e.PrintLine("판매집계표"),
                    e.SetStyles(PrintStyle.None),
                    e.PrintLine("--------------------------"),
                    e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight),
                    e.PrintLine(json["date"].ToString()),
                    e.SetStyles(PrintStyle.None),
                    e.PrintLine("--------------------------")
                ));
            printer.Write(
                ByteSplicer.Combine(
                    e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight | PrintStyle.DoubleWidth),
                        e.LeftAlign(),
                        e.PrintLine("구분 \t\t 건수 \t\t 금액"),
                        e.PrintLine("--------------------------"),
                        e.PrintLine("총매출액 \t\t\t" + json["totalCnt"] + " \t\t " + json["totalPrice"]),
                        e.PrintLine("텀블러할인 \t\t\t" + json["discountCnt"] + " \t\t " + json["discountPrice"]),
                        e.PrintLine("취소 \t\t\t" + json["canceledCnt"] + " \t\t " + json["canceledPrice"]),
                        e.PrintLine("--------------------------"),
                        e.PrintLine("순매출액 \t\t\t" + json["sunCnt"] + " \t\t " + json["sunPrice"]),
                        e.FeedLines(3),
                        e.FullCut()
                    ));

            printer.Dispose();
        }

        // 프린트 함수
        static void StartPrint(JObject json)
        {
            //프린터 연결
#if DEBUG
            printer = new TestSerialPrinter(portName: "COM1", baudRate: 9600);
#else
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);
#endif

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

        static void reprintBill(JObject json)
        {
            //프린터 연결
#if DEBUG
            printer = new TestSerialPrinter(portName: "COM1", baudRate: 9600);
#else
            printer = new SerialPrinter(portName: "COM1", baudRate: 9600);
#endif
///////////////////////////////////////////////////
//       영수증 재출력용 JSON                        // 
//                                               //
//{                                              //
//    action : "reprint",                        //
//    totalPrice : 모든 메뉴 총가격,                 //
//    approvalDate : 승인일자,                     //
//    approvalNum : 승인번호,                      //
//    cardName : 카드이름,                         //
//    cardNum : 카드번호,                          //
//    acqName : 매입사명,                          //
//    menus : {                                 //
//              {                               // 
//                  name : 음료명,                //
//                  totalPrice: 옵션을 합한 가격,   //
//                  quantity : 수량              //
//              }                               //
//            },                                //
//            ...                               //
//                                              //
//}                                             //
//////////////////////////////////////////////////
            // 메뉴 해쉬코드 - 내용 딕셔너리
            Dictionary<int, JToken> menuDictionary = new Dictionary<int, JToken>();

            // 각 메뉴마다 수량 겟하고 내용 저장
            foreach (var menu in json["menus"])
            {
                int menuHash = menu.ToString().GetHashCode();

                if (menuDictionary.ContainsKey(menuHash)){
                    menuDictionary[menuHash]["quantity"] = menuDictionary[menuHash]["quantity"].ToObject<int>() + 1;
                }
                else
                {
                    menu["quantity"] = 1;
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
                e.FeedLines(1),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight),
                e.PrintLine("11호관 커피"),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("울산시 남구 대학로 93 11호관 305호"),
                e.PrintLine("TEL. 052-220-5757"),
                e.FeedLines(1),
                e.SetStyles(PrintStyle.DoubleWidth),
                e.PrintLine("--------------------------"),
                e.SetStyles(PrintStyle.None),
                e.LeftAlign(),
                e.PrintLine("  메뉴\t\t\t수량\t가격"),
                e.CenterAlign(),
                e.SetStyles(PrintStyle.DoubleWidth),
                e.PrintLine("--------------------------")
              ));

            byte[] bodyBytes = e.FeedLines(1);

            foreach (var menu in menuDictionary.Values)
            {
                string menu_name = menu["name"].ToString();
                int menu_quantity = menu["quantity"].ToObject<int>();
                int menu_totalPrice = menu["totalPrice"].ToObject<int>();
            }

            foreach (var menu in menuDictionary.Values)
            {
                byte[] menuBytes = ByteSplicer.Combine(
                    e.SetStyles(PrintStyle.FontB | PrintStyle.Bold),
                    e.PrintLine("  "+menu_name_line+"\t\t\t"+menu_quantity+"\t"+menu_totalPrice),
                );
                bodyBytes = ByteSplicer.Combine(bodyBytes, menuBytes, e.FeedLines(1));
            }

            printer.Write(bodyBytes);
            printer.Write(ByteSplicer.Combine(
                e.CenterAlign(),
                e.SetStyles(PrintStyle.DoubleWidth),
                e.PrintLine("--------------------------"),
                e.FeedLines(1),
                e.SetStyles(PrintStyle.FontB | PrintStyle.DoubleHeight),
                e.PrintLine("신용카드 승인 정보"),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("카드명 : "+json["cardName"].ToString()),
                e.PrintLine("카드번호 : "+json["cardNum"].ToObject<int>()+"**********"),
                e.PrintLine("매입사 : "+json["acqName"].ToString()),
                e.FeedLines(1),
                e.PrintLine("사 업 자 : 691-85-00176 엄문호"),
                e.PrintLine("가맹정명 : 11호관 커피숍"),
                e.PrintLine("가맹번호 : 157431024"),
                e.PrintLine("승인일시 : "+json["approvalDate"]),
                e.PrintLine("승인번호 : "+json["approvalNum"]),
                e.PrintLine("승인금 : "+json["totalPrice"]),
                e.SetStyles(PrintStyle.DoubleWidth),
                e.PrintLine("--------------------------"),
                e.FeedLines(1),
                e.SetStyles(PrintStyle.None),
                e.PrintLine("저희 11호관 커피를 이용해주셔서 감사합니다.")
            ));
            
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
}

