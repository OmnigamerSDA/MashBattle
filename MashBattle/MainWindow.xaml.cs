using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace MashBattle
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        DispatcherTimer countdownTimer;
        DispatcherTimer updateTimer;

        const int BAUD_RATE = 115200;

        //MashSet mashes1;
        //MashSet mashes2;
        SerialComms serial1;
        SerialComms serial2;

        int countval = 5;

        int mashes1 = 0;
        int mashes2 = 0;

        const int THRESHOLD = 10;
        const int RATE = 10;
        const int PERIOD = 2;
        const byte BATTLE = 0xDD;
        const byte START = 0xAA;
        public MainWindow()
        {
            InitializeComponent();

            countdownTimer = new DispatcherTimer(DispatcherPriority.Normal);
            countdownTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            countdownTimer.Tick += new EventHandler(Countdown);

            updateTimer = new DispatcherTimer(DispatcherPriority.Normal);
            updateTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            updateTimer.Tick += new EventHandler(UpdateBattle);

            updatePortList();
        }

        private void UpdateBattle(object sender, EventArgs e)
        {
            mashes2++;

            if (mashes1 - mashes2 > THRESHOLD)
                P1Win();
            else if (mashes2 - mashes1 > THRESHOLD)
                P2Win();
            else
            {
                int val = mashes2 - mashes1;
                double width = arenaPanel.Width;


                

                //Vector offset = VisualTreeHelper.GetOffset(playerOrb);
                //TranslateTransform trans = new TranslateTransform();
                //playerOrb.RenderTransform = trans;
                //DoubleAnimation movex = new DoubleAnimation((val / ((double)THRESHOLD)) * (width / 3.0) + (width / 2.0)-offset.X, TimeSpan.FromMilliseconds(100));
                DoubleAnimation movex = new DoubleAnimation((val / ((double)THRESHOLD)) * (width / 3.0)+(width/2.0)- (playerOrb.Width / 2.0), TimeSpan.FromMilliseconds(500));
                playerOrb.BeginAnimation(Canvas.LeftProperty, movex);
            }
        }

        private void P2Win()
        {
            updateTimer.Stop();
            serial1.Command(SerialComms.ABORT);
            Status("P2 Win!");
        }

        private void P1Win()
        {
            updateTimer.Stop();
            serial1.Command(SerialComms.ABORT);
            Status("P1 Win!");
        }

        private void Countdown(object sender, EventArgs e)
        {
            --countval;
            if (countval > 0)
            {
                countdownLabel.Content = countval;
            }
            else if (countval == 0)
            {
                //countdownTimer.Stop();
                countdownLabel.Content = "GO!";
                StartBattle();
            }
            else if (countval < 0)
            {
                countdownTimer.Stop();
                DoubleAnimation fadeout = new DoubleAnimation(0, new TimeSpan(0, 0, 0, 0, 500));
                countdownLabel.BeginAnimation(OpacityProperty, fadeout);
            }
        }

        private void StartBattle()
        {
            if (serial1 == null)
            {
                Status("Not connected to COM port!");
                return;
            }

            serial1.Command(START);//Write out control data to Arduino
            Status("Start signal sent.");

            serial1.Command(0x00);
            serial1.Command(0x41);

            mashes1 = 0;
            mashes2 = 0;
            updateTimer.Start();
        }

        void updatePortList()
        {
            comItem.Items.Clear();
            MenuItem newItem = new MenuItem();
            newItem.Header = "None";
            newItem.IsCheckable = true;
            newItem.IsChecked = true;
            newItem.Click += new RoutedEventHandler(COMSelect);
            comItem.Items.Add(newItem);
            foreach (string s in SerialPort.GetPortNames())
            {
                comItem.Items.Add(new MenuItem());
                newItem = (MenuItem)comItem.Items.GetItemAt(comItem.Items.Count - 1);
                newItem.Header = s;
                newItem.IsCheckable = true;
                newItem.IsChecked = false;
                newItem.Click += new RoutedEventHandler(COMSelect);
                //comItems.Items.Add(newItem);
            }

            if (serial1 != null)
                if (serial1.isOpen())
                    serial1.Close();
        }

        private void COMSelect(object sender, EventArgs e)
        {
            MenuItem tempItem;

            for (int i = 0; i < comItem.Items.Count; i++)
            {
                tempItem = (MenuItem)comItem.Items.GetItemAt(i);
                tempItem.IsChecked = false;
            }

            tempItem = (MenuItem)sender;
            tempItem.IsChecked = true;
            comItem.Header = tempItem.Header.ToString();

            if (serial1 != null)
                if (serial1.isOpen())
                    serial1.Close();
            if (tempItem.Header.ToString() != "None")
            {
                serial1 = new SerialComms(tempItem.Header.ToString(), BAUD_RATE, PERIOD, ParseMessage, StatsInvoke);
                //serial.StatusDelegate = Status;
                //serial.BarDelegate = Bar;
                //serial.CountdownDelegate = StartCountdown;
                //serial.UpdateDelegate = StatsInvoke;
                //serial.MashDelegate = MashIncrement;
            }
            //comString = tempItem.Header.ToString();

            //statusLine.Content = comString;
        }

        public void StatsInvoke(MashSet mymashes, MashSet mymashes2)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       UpdateStats(mymashes, mymashes2);
                                   }));
        }

        private void UpdateStats(MashSet mymashes, MashSet mymashes2)
        {
            return;
        }

        private void ParseMessage(int code, string message)
        {
            switch (code)
            {
                case SerialComms.STATUS:
                    Status(message);
                    break;
                case SerialComms.ACTIVE:
                    Status(message);
                    //Bar(Brushes.Yellow);
                    break;
                case SerialComms.MASH:
                    Mash(++mashes1);
                    break;
                case SerialComms.STARTED:
                    Status(message);
                    //StartCountdown();
                    break;
                default:
                    Status(message);
                    break;
            }

        }

        public void Status(string v)
        {
            statusLine.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       //statusBox.AppendText(String.Format(v + "\n"));
                                       //statusBox.CaretIndex = statusBox.Text.Length;
                                       //statusBox.ScrollToEnd();
                                       statusLine.Content = v;
                                       //statusBox.AppendText(v+"\n");
                                   }));

        }

        public void Mash(int num)
        {
            mashLabel.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       //statusBox.AppendText(String.Format(v + "\n"));
                                       //statusBox.CaretIndex = statusBox.Text.Length;
                                       //statusBox.ScrollToEnd();
                                       mashLabel.Content = String.Format("Mashes: {0}", num) ;
                                   }));

        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            countdownLabel.Content = 5;
            countval = 5;
            
            DoubleAnimation movex = new DoubleAnimation((arenaPanel.Width / 2.0)-(playerOrb.Width/2.0), TimeSpan.FromMilliseconds(2000));
            playerOrb.BeginAnimation(Canvas.LeftProperty, movex);

            DoubleAnimation fadein = new DoubleAnimation(1, new TimeSpan(0, 0, 0, 0, 500));
            countdownLabel.BeginAnimation(OpacityProperty, fadein);
            
            countdownTimer.Start();
        }

        private void abortButton_Click(object sender, RoutedEventArgs e)
        {
            serial1.Command(0xFF);
        }
    }
}
