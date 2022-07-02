using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


// C#バージョンの確認用
//#error version
// C# 言語バージョン: 7.3

namespace CommTest
{

    // COMポートの　コンボボックス用 
    public class ComPortNameClass
    {
        string _ComPortName;

        public string ComPortName
        {
            get { return _ComPortName; }
            set { _ComPortName = value; }
        }
    }



    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ComPortNameClass> ComPortNames;    // 通信ポート(COM1,COM2等)のコレクション 
                                                                       // データバインドするため、ObservableCollection　
        public static SerialPort serialPort;        // シリアルポート

        byte[] sendBuf;          // 送信バッファ   
        int sendByteLen;         //　送信データのバイト数

        byte[] rcvBuf;           // 受信バッファ
        int srcv_pt;             // 受信データ格納位置

        int data_receive_thread_id;  // データ受信ハンドラのスレッドID
        int data_receive_thread_cnt;  // データ受信ハンドラの実施回数

        int all_data_received;       // 1 :全てのデータ受信済み

        public MainWindow()
        {
            InitializeComponent();

            MainWindow.serialPort = new SerialPort();    // シリアルポートのインスタンス生成
            MainWindow.serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);  // データ受信時のイベント処理

            ComPortNames = new ObservableCollection<ComPortNameClass>();  // 通信ポートのコレクション　インスタンス生成

            ComPortComboBox.ItemsSource = ComPortNames;       // 通信ポートコンボボックスのアイテムソース指定  

            SetComPortName();                // 通信ポート名をコンボボックスへ設定

            if (ComPortNames.Count > 0)     // 通信ポートがある場合
            {
                if (serialPort.IsOpen == true)    // new Confserial()実行時に、 既に Openしている場合
                {
                    OpenInfoTextBox.Text = "(" + serialPort.PortName + ") is opened.";
                    ComPortOpenButton.Content = "Close";      // ボタン表示 Close
                }
                else
                {
                    OpenInfoTextBox.Text = "(" + serialPort.PortName + ") is closed.";
                    ComPortOpenButton.Content = "Open";      // ボタン表示 Close
                }
            }
            else
            {
                OpenInfoTextBox.Text = "COM port is not found.";
            }


            sendBuf = new byte[2048];     // 送信バッファ領域  serialPortのWriteBufferSize =2048 byte(デフォルト)
            rcvBuf = new byte[4096];      // 受信バッファ領域   SerialPort.ReadBufferSize = 4096 byte (デフォルト



            int id = System.Threading.Thread.CurrentThread.ManagedThreadId; // 現在実行しているスレッドのID
            ThreadIdTextBox.Text += "Main WindowのスレッドID : " + id.ToString() + "\n";

        }



        // 通信ポート名をコンボボックスへ設定
        private void SetComPortName()
        {
            ComPortNames.Clear();           // 通信ポートのコレクション　クリア


            string[] PortList = SerialPort.GetPortNames();              // 存在するシリアルポート名が配列の要素として得られる。

            foreach (string PortName in PortList)
            {
                ComPortNames.Add(new ComPortNameClass { ComPortName = PortName }); // シリアルポート名の配列を、コレクションへコピー
            }

            if (ComPortNames.Count > 0)
            {
                ComPortComboBox.SelectedIndex = 0;   // 最初のポートを選択

                ComPortOpenButton.IsEnabled = true;  // ポートOPENボタンを「有効」にする。
            }
            else
            {
                ComPortOpenButton.IsEnabled = false;  // ポートOPENボタンを「無効」にする。
            }

        }


        // Findボタンを押した時の処理
        // 通信ポートの検索ボタン
        //
        private void ComPortSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SetComPortName();
        }


        // Openボタンを押した時の処理
        // 通信ポートのオープン
        //
        //  SerialPort.ReadBufferSize = 4096 byte (デフォルト)
        //             WriteBufferSize =2048 byte
        //
        private void ComPortOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen == true)    // 既に Openしている場合
            {
                try
                {
                    serialPort.Close();

                    OpenInfoTextBox.Text = "Close(" + serialPort.PortName + ")";

                    ComPortComboBox.IsEnabled = true;        // 通信条件等を選択できるようにする。
                    ComPortSearchButton.IsEnabled = true;    // 通信ポート検索ボタンを有効とする。
                    ComPortOpenButton.Content = "Open"; 　　 // ボタン表示を Closeから Openへ

                }
                catch (Exception ex)
                {
                    OpenInfoTextBox.Text = ex.Message;
                }

            }
            else                      // Close状態からOpenする場合
            {
                serialPort.PortName = ComPortComboBox.Text;    // 選択したシリアルポート

                 serialPort.BaudRate = 76800;           // ボーレート 76.8[Kbps]
               // serialPort.BaudRate = 9600;           // ボーレート 9600[bps]
                serialPort.Parity = Parity.None;       // パリティ無し
                serialPort.StopBits = StopBits.One;    //  1 ストップビット

                serialPort.Open();             // シリアルポートをオープンする
                serialPort.DiscardInBuffer();  // 受信バッファのクリア


                ComPortComboBox.IsEnabled = false;        // 通信条件等を選択不可にする。

                ComPortSearchButton.IsEnabled = false;    // 通信ポート検索ボタンを無効とする。

                OpenInfoTextBox.Text = " Open (" + serialPort.PortName + ")";

                ComPortOpenButton.Content = "Close";      // ボタン表示を OpenからCloseへ

            }
        }


        // Test Sendボタンを押した時の処理
        // データの送信
        private void Test_Send_Button_Click(object sender, RoutedEventArgs e)
        {

            if (MainWindow.serialPort.IsOpen == true)
            {
                srcv_pt = 0;                   // 受信データ格納位置クリア
                data_receive_thread_cnt = 0;  // 
                all_data_received = 0;
        
                int.TryParse(SendByteTextBox.Text, out sendByteLen); // 送信バイト数の入力

                for ( byte i = 0; i <sendByteLen; i++)
                {
                    sendBuf[i] = i;
                }

               
                MainWindow.serialPort.Write(sendBuf, 0, sendByteLen);     // データ送信

                for (int i = 0; i < sendByteLen; i++)   // // 送信データの表示
                {
                    if ((i > 0) && (i % 16 == 0))    // 16バイト毎に1行空ける
                    {
                        SendTextBox.Text += "\r\n";
                    }

                    SendTextBox.Text +=  sendBuf[i].ToString("X2") + " ";

                }
                SendTextBox.Text += "(" + DateTime.Now.ToString("HH:mm:ss.fff") + ")" + "\r\n";
            }

            else
            {
                OpenInfoTextBox.Text = "Comm port closed !";
                
            }

        }

        // デリゲート関数の宣言
        private delegate void DelegateFn();

        // データ受信時のイベント処理
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (all_data_received == 1) {  // 既に全データ受信されている場合、リターン
                return;                         
            }

            int rd_num = MainWindow.serialPort.BytesToRead;       // 受信データ数

            MainWindow.serialPort.Read(rcvBuf, srcv_pt, rd_num);   // 受信データを読み出して、受信バッファに格納

            srcv_pt = srcv_pt + rd_num;     // 次回の保存位置
            data_receive_thread_cnt++;      // データ受信ハンドラの実施回数のインクリメント

            int id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            data_receive_thread_id = id;                          //データ受信スレッドのID格納


            if (srcv_pt == sendByteLen)  // 最終データ受信済み (受信データ数は、送信バイト数と同一とする)　イベント処理の終了
            {
                all_data_received = 1;    // 全データ受信済み
                Dispatcher.BeginInvoke(new DelegateFn(RcvProc)); // Delegateを生成して、RcvProcを開始   (表示は別スレッドのため)
            }

        }

        //
        // データ受信イベント終了時の処理
        // 受信データの表示
        //
        private void RcvProc()
        {
            string rcv_str = "";

            ThreadIdTextBox.Text += "データ受信ハンドラの実施回数:" + data_receive_thread_cnt.ToString() + "\n";

            ThreadIdTextBox.Text += "DataReceivedHandlerのスレッドID:" + data_receive_thread_id.ToString() + "\n";

            int id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            ThreadIdTextBox.Text += "RcvProcのスレッドID:" + id.ToString() + "\n";


            for (int i = 0; i < srcv_pt; i++)   // 表示用の文字列作成
            {
               if ((i > 0) && (i % 16 == 0))    // 16バイト毎に1行空ける
               {
                    rcv_str = rcv_str + "\r\n";
               }

               rcv_str = rcv_str + rcvBuf[i].ToString("X2") + " ";
            }

             RcvTextBox.Text = RcvTextBox.Text + rcv_str;  // 受信文と時刻の表示

             RcvTextBox.Text += "(" + DateTime.Now.ToString("HH:mm:ss.fff") + ")(" + srcv_pt.ToString() + " bytes )" + "\r\n";



        }

        
        // クリアボタンを押した時の処理
        private void Clear_Button_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Text = "";
            RcvTextBox.Text = "";
            ThreadIdTextBox.Text = "";
        }
    }
}
