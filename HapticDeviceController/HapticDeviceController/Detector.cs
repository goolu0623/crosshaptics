using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using bHapticsLibEndpoint;
using bHapticsLib;

namespace HapticDeviceController {
    class HapticEventsHandler {

        // 原本的??
        private int DELAY_TIME_WINDOW = 10; // Delay time to detect symmetric signals


        //bhapticslibs
        private VestController vestController = new VestController();

        // switches for solution
        public bool SYM_enabled=false;

        private struct HapticEvent {
            // 性質
            public float Duration; // 這三個基本資訊合起來是 channel 裡的 stateinfo 
            public float Amplitude;
            public float Frequency;
            public DateTime EventDayTime;// new CultureInfo("en-US");

            public string EventType;
            public string EventTime;
            public string EventName;
            public string SourceTypeName;

            public DateTime EnListTime;


            public HapticEvent(string EventTime, string SourceTypeName, string EventType, string EventName, string Amp, string Freq, string Dur, DateTime EnListTime) {
                this.Duration = Convert.ToSingle(Dur);
                this.Amplitude = Convert.ToSingle(Amp);
                this.Frequency = Convert.ToSingle(Freq);

                this.EventDayTime = DateTime.Parse(EventTime, new CultureInfo("en-US"), DateTimeStyles.NoCurrentDateDefault);
                this.EventTime = EventTime;
                this.EventType = EventType;
                this.EventName = EventName;
                this.SourceTypeName = SourceTypeName;

                this.EnListTime = EnListTime;

            }
        };
        private LinkedList<HapticEvent> eventList = new LinkedList<HapticEvent>();
        private bool IsSymmetric() {
            // 檢查第一個有沒有跟人對稱
            lock (eventList) {
                var iterate_node = eventList.First;
                var nowEvent = iterate_node.Value;
                while (iterate_node.Next != null) {
                    var next_node = iterate_node.Next;
                    var nextEvent = next_node.Value;
                    if (nextEvent.Duration == nowEvent.Duration
                        && nextEvent.Amplitude == nowEvent.Amplitude
                        && nextEvent.Frequency == nowEvent.Frequency
                        && nextEvent.SourceTypeName != nowEvent.SourceTypeName) {
                        // 拔掉對稱
                        eventList.Remove(next_node);
                        return true;

                    }
                    iterate_node = next_node;
                }
            }

            return false;
        }

        public void Router(string msg) {
            // 解析msg
            string[] haptic_event_info_lists = msg.Split('|');
            string[] state_info_list = haptic_event_info_lists[4].Split(' ');
            // 做成HapticEvent
            HapticEvent temp = new HapticEvent(
                EventTime: haptic_event_info_lists[0],
                SourceTypeName: haptic_event_info_lists[1],
                EventType: haptic_event_info_lists[2],
                EventName: haptic_event_info_lists[3],
                Amp: state_info_list[1],
                Freq: state_info_list[3],
                Dur: state_info_list[5],
                EnListTime: DateTime.Now
            );




            // add into eventList
            lock (eventList) {
                eventList.AddLast(temp);
            }

        }
        
        public bool Ischange = false; 
        public void mainThread() {
            //System.Threading.Thread.CurrentThread.IsBackground = true;
            //bHapticsLib Vest Init
            vestController.Init();

            HapticEvent nowEvent; // 後面會一直用到 在這邊宣告應該比較不浪費宣告又release的時間





            while (true) {

                
                // 看list裡面的時間過期沒
                while (eventList.Count() != 0) {
                    DateTime nowTime = DateTime.Now;
                    DateTime eventTime = nowTime;

                    lock (eventList) {
                        nowEvent = eventList.First();
                    }



                    // 給個time window XX ms 看這個區間有沒有產生對稱震動
                    if ((nowTime - nowEvent.EnListTime).TotalMilliseconds < DELAY_TIME_WINDOW)
                        break;


                    //eventList.RemoveFirst();
                    if (SYM_enabled == true){
                        if (IsSymmetric()) {
                            Console.WriteLine($"SYM  {nowEvent.EventDayTime.ToString("MM/dd HH:mm:ss.fff", new CultureInfo("en-US")) + "  :"}|{nowEvent.SourceTypeName}|{nowEvent.Amplitude}|{nowEvent.Frequency}|{nowEvent.Duration}|{nowEvent.EventName}");
                            sym_pattern_1(ref nowEvent);
                        }
                        else {
                        // Console.WriteLine(eventTime + ": " + "None Amp = " + nowEvent.Amplitude + " Dur = " + nowEvent.Duration + " Freq = " + nowEvent.Frequency);
                        // send 原本controller震動 (這邊我們不disable的話可以do nothing, 就不用remap回去了)
                            Console.WriteLine($"NONE {nowEvent.EventDayTime.ToString("MM/dd HH:mm:ss.fff", new CultureInfo("en-US")) + "  :"}|{nowEvent.SourceTypeName}|{nowEvent.Amplitude}|{nowEvent.Frequency}|{nowEvent.Duration}|{nowEvent.EventName}");
                            nonesym_pattern_1(ref nowEvent);
                        }

                    }
                    else{
                        Console.WriteLine($"NONE {nowEvent.EventDayTime.ToString("MM/dd HH:mm:ss.fff", new CultureInfo("en-US")) + "  :"}|{nowEvent.SourceTypeName}|{nowEvent.Amplitude}|{nowEvent.Frequency}|{nowEvent.Duration}|{nowEvent.EventName}");
                        nonesym_pattern_1(ref nowEvent);
                    }
                    lock (eventList) {
                        eventList.RemoveFirst();
                    }
                    
                }
                // 睡個1ms再來一次 免得他一直跑 block掉其他人
                // System.Threading.Thread.Sleep(1);
            }
        }

        private void sym_pattern_1(ref HapticEvent nowEvent) {
            // 直接對應一模一樣的pattern到vest上
            vestController.Play(
                amp: nowEvent.Amplitude,
                freq: nowEvent.Frequency,
                dur: nowEvent.Duration,
                body_side: BodySide.both
            );
            return;
        }

        private void nonesym_pattern_1(ref HapticEvent nowEvent) {
            // 
            if (nowEvent.Amplitude > 0.9f && nowEvent.SourceTypeName == "LeftController") {
                vestController.Play(
                    amp: nowEvent.Amplitude,
                    freq: nowEvent.Frequency,
                    dur: nowEvent.Duration,
                    body_side: BodySide.left
                );
            }
            else if (nowEvent.Amplitude > 0.9f && nowEvent.SourceTypeName == "RightController") {
                vestController.Play(
                    amp: nowEvent.Amplitude,
                    freq: nowEvent.Frequency,
                    dur: nowEvent.Duration,
                    body_side: BodySide.right
                );
            }
            return;
        }

        public void Switch_Solution_Mode()
        {
            SYM_enabled = (SYM_enabled) ? false : true;
            Console.WriteLine("SYM_enabled: " + SYM_enabled);
        }

        public bool KeyCheck() {
            if (!Console.KeyAvailable)
                return false;

            
            ConsoleKeyInfo key = Console.ReadKey(true);
            ScaleOption temp = new ScaleOption();
            switch (key.Key) {
                case ConsoleKey.Enter:
                    return true;

                case ConsoleKey.P:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.1f, 100f, 0.04f, BodySide.both);
                    Console.WriteLine("get P");
                    goto default;


                case ConsoleKey.N:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(1f, 100f, 0.04f, BodySide.both);
                    Console.WriteLine("get N");

                    goto default;

                case ConsoleKey.M:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.5f, 100f, 0.04f, BodySide.both);
                    Console.WriteLine("get M");
                    goto default;

                case ConsoleKey.J:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(1f, 100f, 1f, BodySide.both);
                    Console.WriteLine("get J");
                    goto default;
                case ConsoleKey.Z:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.1f, 100f, 0.04f, BodySide.both);
                    Console.WriteLine("get Z");
                    goto default;
                case ConsoleKey.X:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.2f, 100f, 0.03f, BodySide.both);
                    Console.WriteLine("get X");
                    goto default;
                case ConsoleKey.C:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.5f, 100f, 0.02f, BodySide.both);
                    Console.WriteLine("get C");
                    goto default;
                case ConsoleKey.V:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(1f, 100f, 0.001f, BodySide.both);
                    Console.WriteLine("get V");
                    goto default;
                case ConsoleKey.A:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(1f, 320f, 1f, BodySide.both);
                    Console.WriteLine("get A");
                    goto default;
                case ConsoleKey.S:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.2f, 200f, 0.001f, BodySide.both);
                    Console.WriteLine("get S");
                    goto default;
                case ConsoleKey.D:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(0.5f, 200f, 0.0005f, BodySide.both);
                    Console.WriteLine("get D");
                    goto default;
                case ConsoleKey.F:
                    temp.Intensity = 0.1f;
                    temp.Duration = 1f;
                    vestController.Play(1f, 200f, 0.0001f, BodySide.both);
                    Console.WriteLine("get F");
                    goto default;


                default:
                    return false;
            }
        }

       
    }

}
