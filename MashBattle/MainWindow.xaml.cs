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
using OxyPlot;
using OxyPlot.Series;
using System.Resources;

namespace MashBattle
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class BattleWindow : Window
    {
        bool oneplayer = false;

        bool twobutton = false;

        PlayerForm subForm;

        DispatcherTimer countdownTimer;
        DispatcherTimer updateTimer;
        DispatcherTimer delayTimer;

        const int BAUD_RATE = 115200;

        SerialComms serial1;
        SerialComms serial2;

        SheetsAgent sheets;

        int intervals = 0;

        int countval = 5;

        int mashes1 = 0;
        int mashes2 = 0;

        const int THRESHOLD = 10;
        const int RATE = 10;
        const int PERIOD = 2;
        const byte BATTLE = 0xDD;
        const byte START = 0xAA;

        string input1 = "ARC";
        string input2 = "ARC";

        bool plot_ready = false;

        LineSeries p1Series;

        LineSeries p2Series;

        public BattleWindow()
        {
            InitializeComponent();

            sheets = new SheetsAgent();

            countdownTimer = new DispatcherTimer(DispatcherPriority.Normal);
            countdownTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            countdownTimer.Tick += new EventHandler(Countdown);

            updateTimer = new DispatcherTimer(DispatcherPriority.Normal);
            updateTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            updateTimer.Tick += new EventHandler(UpdateBattle);

            delayTimer = new DispatcherTimer(DispatcherPriority.Normal);
            delayTimer.Interval = new TimeSpan(0, 0, 0, 2, 0);
            delayTimer.Tick += new EventHandler(PlotResults);

            updatePortList();
            PlayerUpdate();
        }

        private void UpdateBattle(object sender, EventArgs e)
        {
            intervals++;
            //Mash2(++mashes2);
            //p2mash1.AddMash(40, 60);
            if(oneplayer)
                Mash2(++mashes2);

            p1Series.Points.Add(new OxyPlot.DataPoint(((double)intervals)*.1, Math.Round(mashes1/(((double)intervals) * .1),2)));
            p2Series.Points.Add(new OxyPlot.DataPoint(((double)intervals) * .1, Math.Round(mashes2 / (((double)intervals) * .1),2)));

            if (mashes1 - mashes2 > THRESHOLD)
                P1Win();
            else if (mashes2 - mashes1 > THRESHOLD)
                P2Win();
            else
            {
                int val = mashes1 - mashes2;
                //double width = arenaPanel.Width;

                //Vector offset = VisualTreeHelper.GetOffset(playerOrb);
                //TranslateTransform trans = new TranslateTransform();
                //playerOrb.RenderTransform = trans;
                //DoubleAnimation movex = new DoubleAnimation((val / ((double)THRESHOLD)) * (width / 3.0) + (width / 2.0)-offset.X, TimeSpan.FromMilliseconds(100));
                //DoubleAnimation movex = new DoubleAnimation((val / ((double)THRESHOLD)) * (width / 3.0)+(width/2.0)- (playerOrb.Width / 2.0), TimeSpan.FromMilliseconds(500));
                //playerOrb.BeginAnimation(Canvas.LeftProperty, movex);

                //DoubleAnimation barX = new DoubleAnimation(((val+THRESHOLD) / ((double)THRESHOLD*2))*100, TimeSpan.FromMilliseconds(500));
                //mygauge.BeginAnimation(Yuhan.WPF.Controls.GaugeBar.PercentageProperty,barX);

                DoubleAnimation gaugeX = new DoubleAnimation(((val+THRESHOLD) / ((double)THRESHOLD*2))*20, TimeSpan.FromMilliseconds(500));
                myGauge.BeginAnimation(CircularGauge.CircularGaugeControl.CurrentValueProperty,gaugeX);

                myGauge.BeginAnimation(CircularGauge.CircularGaugeControl.OptimalRangeStartValueProperty, gaugeX);
            }
        }

        private void P2Win()
        {
            updateTimer.Stop();
            serial1.Command(SerialComms.ABORT);
            if (!oneplayer)
                serial2.Command(SerialComms.ABORT);
            Status("P2 Win!");
            LabelUpdate(2);
            delayTimer.Start();

            sheets.SaveBattle(twobutton,p1_nameBox.Text, p2_nameBox.Text, false, intervals, mashes1, mashes2);
        }

        private void P1Win()
        {
            updateTimer.Stop();
            serial1.Command(SerialComms.ABORT);
            if (!oneplayer)
                serial2.Command(SerialComms.ABORT);
            Status("P1 Win!");
            LabelUpdate(1);
            delayTimer.Start();

            sheets.SaveBattle(twobutton,p1_nameBox.Text, p2_nameBox.Text, true, intervals, mashes1, mashes2);
        }

        public void LabelUpdate(int val)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       DoubleAnimation fadein = new DoubleAnimation(1, new TimeSpan(0, 0, 0, 0, 500));
                                       countdownLabel.BeginAnimation(OpacityProperty, fadein);

                                       if (val == 1)
                                           countdownLabel.Content = String.Format("{0} wins!", p1_nameBox.SelectedValue.ToString());
                                       else
                                           countdownLabel.Content = String.Format("{0} wins!", p2_nameBox.SelectedValue.ToString());
                                   }));
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

            if (serial2 == null && !oneplayer)
            {
                Status("Not connected to COM port!");
                return;
            }

            serial1.Command(START);//Write out control data to Arduino
            Status("Start signal sent.");

            serial1.Command(0x00);
            if(twobutton)
                serial1.Command(MashAttack.Config.GetCode(input1,0,1));
            else
                serial1.Command(MashAttack.Config.GetCode(input1, 0));

            if (!oneplayer)
            {
                serial2.Command(START);//Write out control data to Arduino
                Status("Start signal sent.");

                serial2.Command(0x00);
                if (twobutton)
                    serial2.Command(MashAttack.Config.GetCode(input2, 0, 1));
                else
                    serial2.Command(MashAttack.Config.GetCode(input2, 0));
            }

            //p1mash1 = new MashSet();
            //p2mash1 = new MashSet();

            //if (twobutton)
            //{
            //    p1mash2 = new MashSet();
            //    p2mash2 = new MashSet();
            //}

            p1Series = new LineSeries { StrokeThickness = 5, Color = OxyColors.Purple, MarkerSize = 3, MarkerStroke = OxyColors.ForestGreen, MarkerType = MarkerType.Plus };

            p2Series = new LineSeries { StrokeThickness = 5, Color = OxyColors.OrangeRed, MarkerSize = 3, MarkerStroke = OxyColors.ForestGreen, MarkerType = MarkerType.Plus };

            intervals = 0;
            mashes1 = 0;
            mashes2 = 0;
            updateTimer.Start();
        }

        private void PlayerUpdate()
        {
            p1_nameBox.Items.Clear();
            p2_nameBox.Items.Clear();
            List<string> users = sheets.GetUsernames();
            for (int i = 0; i < users.Count; i++)
            {
                p1_nameBox.Items.Add(users[i]);
                p2_nameBox.Items.Add(users[i]);
            }
            p1_nameBox.SelectedIndex = 0;
            p2_nameBox.SelectedIndex = 0;
        }

        void updatePortList()
        {
            comItem.Items.Clear();
            comItem2.Items.Clear();
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


            newItem = new MenuItem();
            newItem.Header = "None";
            newItem.IsCheckable = true;
            newItem.IsChecked = true;
            newItem.Click += new RoutedEventHandler(COMSelect2);
            comItem2.Items.Add(newItem);
            foreach (string s in SerialPort.GetPortNames())
            {
                comItem2.Items.Add(new MenuItem());
                newItem = (MenuItem)comItem2.Items.GetItemAt(comItem2.Items.Count - 1);
                newItem.Header = s;
                newItem.IsCheckable = true;
                newItem.IsChecked = false;
                newItem.Click += new RoutedEventHandler(COMSelect2);
                //comItems.Items.Add(newItem);
            }

            if (serial2 != null)
                if (serial2.isOpen())
                    serial2.Close();
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

            if (tempItem.Header != comItem2.Header)
            {
                tempItem.IsChecked = true;
                comItem.Header = tempItem.Header.ToString();

                if (serial1 != null)
                    if (serial1.isOpen())
                        serial1.Close();

                try
                {
                    if (tempItem.Header.ToString() != "None")
                    {
                        serial1 = new SerialComms(tempItem.Header.ToString(), BAUD_RATE, PERIOD, ParseMessage1, StatsInvoke1);
                        //serial.StatusDelegate = Status;
                        //serial.BarDelegate = Bar;
                        //serial.CountdownDelegate = StartCountdown;
                        //serial.UpdateDelegate = StatsInvoke;
                        //serial.MashDelegate = MashIncrement;
                    }
                }
                catch
                {
                    Status("Failed to open Port! Is it already in use?");
                }
                //comString = tempItem.Header.ToString();
            }
            //statusLine.Content = comString;
        }

        private void COMSelect2(object sender, EventArgs e)
        {
            MenuItem tempItem;

            for (int i = 0; i < comItem.Items.Count; i++)
            {
                tempItem = (MenuItem)comItem2.Items.GetItemAt(i);
                tempItem.IsChecked = false;
            }

            tempItem = (MenuItem)sender;

            if (tempItem.Header.ToString() != comItem.Header.ToString())
            {
                tempItem.IsChecked = true;
                comItem2.Header = tempItem.Header.ToString();

                if (serial2 != null)
                    if (serial2.isOpen())
                        serial2.Close();

                try
                {
                    if (tempItem.Header.ToString() != "None")
                    {
                        serial2 = new SerialComms(tempItem.Header.ToString(), BAUD_RATE, PERIOD, ParseMessage2, StatsInvoke1);
                        //serial.StatusDelegate = Status;
                        //serial.BarDelegate = Bar;
                        //serial.CountdownDelegate = StartCountdown;
                        //serial.UpdateDelegate = StatsInvoke;
                        //serial.MashDelegate = MashIncrement;
                    }
                }
                catch
                {
                    Status("Failed to open Port! Is it already in use?");
                }
            }
            //comString = tempItem.Header.ToString();

            //statusLine.Content = comString;
        }

        private void Input1Select(object sender, RoutedEventArgs e)
        {
            MenuItem tempItem;

            for (int i = 0; i < p1_Input.Items.Count; i++)
            {
                tempItem = (MenuItem)p1_Input.Items.GetItemAt(i);
                tempItem.IsChecked = false;
            }

            tempItem = (MenuItem)sender;
            tempItem.IsChecked = true;
            p1_Input.Header = tempItem.Header.ToString();
            input1 = tempItem.Header.ToString();
        }

        private void Input2Select(object sender, RoutedEventArgs e)
        {
            MenuItem tempItem;

            for (int i = 0; i < p2_Input.Items.Count; i++)
            {
                tempItem = (MenuItem)p2_Input.Items.GetItemAt(i);
                tempItem.IsChecked = false;
            }

            tempItem = (MenuItem)sender;
            tempItem.IsChecked = true;
            p2_Input.Header = tempItem.Header.ToString();
            input2 = tempItem.Header.ToString();
        }

        public void StatsInvoke1(MashSet mymashes, MashSet mymashes2)
        {
            //this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
            //                       new Action(delegate ()
            //                       {
            //                           UpdateStats1(mymashes, mymashes2);
            //                       }));
        }

        //public void StatsInvoke2(MashSet mymashes, MashSet mymashes2)
        //{
        //    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
        //                           new Action(delegate ()
        //                           {
        //                               UpdateStats2(mymashes, mymashes2);
        //                           }));
        //}

        private void ParseMessage1(int code, string message)
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
                    Mash1(++mashes1);
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

        private void ParseMessage2(int code, string message)
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
                    Mash2(++mashes2);
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

        //public void UpdateStats1(MashSet newmashes, MashSet newmashes2)
        //{
        //    p1mash1 = newmashes;
        //    p1mash2 = newmashes2;
        //    //sheets.SaveSession(newStats, usersBox.SelectedValue.ToString(), input, mode);
        //    //if (plot_ready)
        //        //PlotResults();
        //    //else
        //    //{
        //    //    plot_ready = true;
        //    //}


        //}

        //public void UpdateStats2(MashSet newmashes, MashSet newmashes2)
        //{
        //    p2mash1 = newmashes;
        //    p2mash2 = newmashes2;
        //    //sheets.SaveSession(newStats, usersBox.SelectedValue.ToString(), input, mode);
        //    //if (plot_ready)
        //    //    PlotResults();
        //    //else
        //    //{
        //    //    plot_ready = true;
        //    //}
        //}

        private void PlotResults(object sender, EventArgs e)
        {
            delayTimer.Stop();
            //plot_ready = false;
            PlotModel tmp = new PlotModel();

            //LineSeries p1Series = new LineSeries { StrokeThickness = 2, Color = OxyColors.Purple, MarkerSize = 3, MarkerStroke = OxyColors.ForestGreen, MarkerType = MarkerType.Plus };
            //LineSeries p1med = new LineSeries { StrokeThickness = 1, Color = OxyColors.MidnightBlue, LineStyle = LineStyle.Dash, MarkerType = MarkerType.None };
            //LineSeries p2Series = new LineSeries { StrokeThickness = 2, Color = OxyColors.OrangeRed, MarkerSize = 3, MarkerStroke = OxyColors.ForestGreen, MarkerType = MarkerType.Plus };
            //LineSeries p2med = new LineSeries { StrokeThickness = 1, Color = OxyColors.DarkRed, LineStyle = LineStyle.Dash, MarkerType = MarkerType.None };

            //int i;
            //long test1 = 0;
            //long test2 = 0;
            //long timestamp1 = 0;
            //long timestamp2 = 0;

            //for (i = 0; i < p1mash1.count; i++)
            //{
            //    test1 = p1mash1.GetMash(i);
            //    timestamp1 += test1;
            //    //Console.WriteLine(test);
            //    p1Series.Points.Add(new OxyPlot.DataPoint(Math.Round(timestamp1 / 1000.0, 3), Math.Round(1000.0 / test1, 2)));
            //    //p2Series.Points.Add(new OxyPlot.DataPoint(Math.Round(timestamp2 / 1000.0, 3), Math.Round(1000.0 / test2, 2)));
            //}

            //for (i = 0; i < p2mash1.count; i++)
            //{
            //    //test1 = p1mash1.GetMash(i);
            //    test2 = p2mash1.GetMash(i);
            //    //timestamp1 += test1;
            //    timestamp2 += test2;
            //    //Console.WriteLine(test);
            //    //p1Series.Points.Add(new OxyPlot.DataPoint(Math.Round(timestamp1 / 1000.0, 3), Math.Round(1000.0 / test1, 2)));
            //    p2Series.Points.Add(new OxyPlot.DataPoint(Math.Round(timestamp2 / 1000.0, 3), Math.Round(1000.0 / test2, 2)));
            //}

            //var valueAxis = new LogarithmicAxis{ MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot, Title = "Frequency (Hz)" };
            //tmp.Axes.Add(valueAxis);
            tmp.Series.Add(p1Series);
            tmp.Series.Add(p2Series);


            //if (twobutton)
            //{
            //    LineSeries mySeries2 = new LineSeries { StrokeThickness = 2, Color = OxyColors.Aqua, MarkerSize = 3, MarkerStroke = OxyColors.Crimson, MarkerType = MarkerType.Triangle };

            //    test = 0;
            //    timestamp = 0;

            //    for (i = 0; i < mashes2.count; i++)
            //    {
            //        test = mashes2.GetMash(i);
            //        timestamp += test;
            //        //Console.WriteLine(test);
            //        mySeries2.Points.Add(new OxyPlot.DataPoint(Math.Round(timestamp / 1000.0, 3), Math.Round(1000.0 / test, 2)));

            //    }



            //    tmp.Series.Add(mySeries2);

            //}

            //if (!twobutton)
            //{
            //    p1med.Points.Add(new OxyPlot.DataPoint(0.00, Math.Round(1000.0 / p1med, 2)));
            //    p1med.Points.Add(new OxyPlot.DataPoint(Math.Ceiling(timestamp1 / 1000.00), Math.Round(1000.0 / median, 2)));
            //    tmp.Series.Add(medLine);
            //}
            //else
            //{
            //    //LineSeries medLine2 = new LineSeries { StrokeThickness = 1, Color = OxyColors.DarkBlue, LineStyle = LineStyle.Dash, MarkerType = MarkerType.None };
            //    //double avg_median = 1000.00 / ((median + median2) / 2);
            //    //double effective_median = 1000.00 / ((median + median2) / 4);

            //    //medLine2.Points.Add(new OxyPlot.DataPoint(0.00, Math.Round(effective_median, 2)));
            //    //medLine2.Points.Add(new OxyPlot.DataPoint(Math.Ceiling(timestamp / 1000.00), Math.Round(effective_median, 2)));
            //    //medLine.Points.Add(new OxyPlot.DataPoint(0.00, Math.Round(avg_median, 2)));
            //    //medLine.Points.Add(new OxyPlot.DataPoint(Math.Ceiling(timestamp / 1000.00), Math.Round(avg_median, 2)));
            //    //tmp.Series.Add(medLine2);
            //    //tmp.Series.Add(medLine);
            //}



            chart.Model = tmp;
            DoubleAnimation fadein = new DoubleAnimation(1, new TimeSpan(0, 0, 0, 0, 500));
            //DoubleAnimation fadeout = new DoubleAnimation(0, new TimeSpan(0, 0, 0, 0, 500));
            //myGauge.Visibility = Visibility.Hidden;
            //myGauge.BeginAnimation(OpacityProperty, fadeout);
            //chart.Visibility = Visibility.Visible;
            chart.BeginAnimation(OpacityProperty, fadein);
            chart.Model.DefaultYAxis.Title = "Frequency (Hz)";
            chart.Model.DefaultXAxis.Title = "Time (s)";
            chart.Model.DefaultYAxis.TitleFontSize = 24;
            chart.Model.DefaultXAxis.TitleFontSize = 24;
            chart.Model.DefaultYAxis.FontSize = 18;
            chart.Model.DefaultXAxis.FontSize = 18;
            chart.Model.DefaultYAxis.MajorStep = 2;
            chart.Model.DefaultYAxis.MinorStep = 1;
            chart.Model.DefaultXAxis.Minimum = .5;
            //chart.Model.DefaultYAxis.Minimum = 0;
            //chart.Model.DefaultYAxis.Maximum = 20;
            chart.Model.ResetAllAxes();

            chart.UpdateLayout();
            chart.IsEnabled = true;

            //myModel.
            //chart.
        }

        public void Mash1(int num)
        {
            mashLabel1.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       //statusBox.AppendText(String.Format(v + "\n"));
                                       //statusBox.CaretIndex = statusBox.Text.Length;
                                       //statusBox.ScrollToEnd();
                                       mashLabel1.Content = String.Format("{0}", num) ;
                                   }));

        }

        public void Mash2(int num)
        {
            mashLabel2.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                   new Action(delegate ()
                                   {
                                       //statusBox.AppendText(String.Format(v + "\n"));
                                       //statusBox.CaretIndex = statusBox.Text.Length;
                                       //statusBox.ScrollToEnd();
                                       mashLabel2.Content = String.Format("{0}", num);
                                   }));

        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            countdownLabel.Content = 5;
            countval = 5;
            
            //DoubleAnimation movex = new DoubleAnimation((arenaPanel.Width / 2.0)-(playerOrb.Width/2.0), TimeSpan.FromMilliseconds(2000));
            //playerOrb.BeginAnimation(Canvas.LeftProperty, movex);

            DoubleAnimation moveX1 = new DoubleAnimation(10, TimeSpan.FromMilliseconds(2000));
            myGauge.BeginAnimation(CircularGauge.CircularGaugeControl.CurrentValueProperty, moveX1);
            myGauge.BeginAnimation(CircularGauge.CircularGaugeControl.OptimalRangeStartValueProperty, moveX1);

            //DoubleAnimation moveX2 = new DoubleAnimation(50, TimeSpan.FromMilliseconds(2000));
            //mygauge.BeginAnimation(Yuhan.WPF.Controls.GaugeBar.PercentageProperty, moveX2);

            DoubleAnimation fadein = new DoubleAnimation(1, new TimeSpan(0, 0, 0, 0, 500));
            DoubleAnimation fadeout = new DoubleAnimation(0, new TimeSpan(0, 0, 0, 0, 500));
            myGauge.BeginAnimation(OpacityProperty, fadein);
            chart.BeginAnimation(OpacityProperty, fadeout);
            countdownLabel.BeginAnimation(OpacityProperty, fadein);

            mashLabel1.Content = 0;
            mashLabel2.Content = 0;

            countdownTimer.Start();
        }

        private void abortButton_Click(object sender, RoutedEventArgs e)
        {
            serial1.Command(0xFF);
        }

        private void playerUpdate_Click(object sender, RoutedEventArgs e)
        {
            PlayerUpdate();
        }

        private void window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //banner.Source = new BitmapImage(new Uri("Resources\\mash-head-to-head-01.png"));
            //banner.Source = "Resources\mash_head_to_head_01.png";
            //banner.Source = new BitmapImage(new Uri("/Resources/mash-head-to-head.png", UriKind.Relative));
            banner.Source = new BitmapImage(new Uri("C:\\MashAttack\\mash-battle-03.png", UriKind.Absolute));
            myGauge.ImageSource = new BitmapImage(new Uri("C:\\MashAttack\\mash-icon-ver-02.png", UriKind.Absolute));

            optionItem.Margin = new Thickness(banner.ActualWidth * .33, 0, 0, 0);
            comItem2.Margin = new Thickness(banner.ActualWidth * .24, 0, 0, 0);

            myGauge.Radius = p1_rec.ActualHeight*.70;
            myGauge.Margin = new Thickness(0,window.ActualHeight - 187 - myGauge.Radius,0,0);
            myGauge.ImageOffset = myGauge.Radius*-.4;
            myGauge.ImageSize = new Size(myGauge.Radius * .4, myGauge.Radius * .6);
            myGauge.RangeIndicatorThickness = myGauge.Radius*.8;
            myGauge.ScaleRadius = myGauge.Radius*.9;
            myGauge.MajorTickSize = new Size(myGauge.Radius * .11, myGauge.Radius * .025);
            myGauge.MinorTickSize = new Size(myGauge.Radius * .05, myGauge.Radius * .015);
        }

        private void p2ledItem_Click(object sender, RoutedEventArgs e)
        {
            if (serial2 != null)
                if (serial2.isOpen())
                    serial2.Command(SerialComms.LEDS);
        }

        private void p1ledItem_Click(object sender, RoutedEventArgs e)
        {
            if (serial1 != null)
                if (serial1.isOpen())
                    serial1.Command(SerialComms.LEDS);
        }

        private void twobuttonItem_Click(object sender, RoutedEventArgs e)
        {
            twobutton = !twobutton;
        }

        private void addPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (subForm == null || !subForm.IsLoaded)
            {
                subForm = new PlayerForm(this);
                subForm.Show();
            }
        }

        public void AddPlayer(string newname)
        {
            sheets.AddUsername(newname);
            PlayerUpdate();
        }

        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (subForm != null)
                subForm.Close();
        }

        private void comUpdateItem_Click(object sender, RoutedEventArgs e)
        {
            updatePortList();
        }



        
    }
}
