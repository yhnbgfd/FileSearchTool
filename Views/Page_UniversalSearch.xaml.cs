using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using F = System.Windows.Forms;

namespace FileSearchTool.Views
{
    public partial class Page_UniversalSearch : Page, INotifyPropertyChanged
    {
        private readonly object _lock = new object();
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public event PropertyChangedEventHandler PropertyChanged;

        private string _searchPath = Properties.Settings.Default.SearchPath;
        private string _searchPattern = Properties.Settings.Default.SearchPattern;
        private string _searchTextFirst = Properties.Settings.Default.SearchTextFirst;
        private string _searchTextEnd = Properties.Settings.Default.SearchTextEnd;
        private int _searchWarnNum = Properties.Settings.Default.SearchWarnNum;
        private string _logText;

        public string SearchPath
        {
            get
            {
                return _searchPath;
            }

            set
            {
                _searchPath = value;
                NotifyPropertyChanged("SearchPath");

                Properties.Settings.Default.SearchPath = SearchPath;
                Properties.Settings.Default.Save();
            }
        }
        public string SearchPattern
        {
            get
            {
                return _searchPattern;
            }

            set
            {
                _searchPattern = value;
                NotifyPropertyChanged("SearchPattern");

                Properties.Settings.Default.SearchPattern = SearchPattern;
                Properties.Settings.Default.Save();
            }
        }
        public string SearchTextFirst
        {
            get
            {
                return _searchTextFirst;
            }

            set
            {
                _searchTextFirst = value;
                NotifyPropertyChanged("SearchTextFirst");

                Properties.Settings.Default.SearchTextFirst = SearchTextFirst;
                Properties.Settings.Default.Save();
            }
        }
        public string SearchTextEnd
        {
            get
            {
                return _searchTextEnd;
            }

            set
            {
                _searchTextEnd = value;
                NotifyPropertyChanged("SearchTextEnd");

                Properties.Settings.Default.SearchTextEnd = SearchTextEnd;
                Properties.Settings.Default.Save();
            }
        }
        public string LogText
        {
            get
            {
                return _logText;
            }

            set
            {
                _logText = value; NotifyPropertyChanged("LogText");
            }
        }

        public int SearchWarnNum
        {
            get
            {
                return _searchWarnNum;
            }

            set
            {
                _searchWarnNum = value;
                NotifyPropertyChanged("SearchWarnNum");

                Properties.Settings.Default.SearchWarnNum = SearchWarnNum;
                Properties.Settings.Default.Save();
            }
        }

        public Page_UniversalSearch()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ShowMsg(string msg, params string[] args)
        {
            msg = string.Format(msg, args);

            _logger.Info(msg);

            string finallyMsg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  #  " + msg + "\r\n";

            LogText += (finallyMsg);
        }

        private void Button_选择文件夹_Click(object sender, RoutedEventArgs e)
        {
            F.FolderBrowserDialog fb = new F.FolderBrowserDialog();
            if (fb.ShowDialog() == F.DialogResult.OK)
            {
                SearchPath = fb.SelectedPath;
            }
        }

        private void Button_开始_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchPath))
            {
                ShowMsg("路径不能为空，已停止扫描。");
                return;
            }
            if (string.IsNullOrWhiteSpace(SearchTextFirst))
            {
                ShowMsg("检索内容开始部分不能为空，已停止扫描。");
                return;
            }

            ShowMsg("开始扫描>>>");

            string oper = (ComboBox_报警行数运算符.SelectedItem as ComboBoxItem).Content.ToString();
            string encoding = (Combobox_编码.SelectedItem as ComboBoxItem).Content.ToString();

            ShowMsg("检索目录/文件：{0},{1}", SearchPath, SearchPattern);
            ShowMsg("检索条件：{0},{1},{2},{3},{4}", SearchTextFirst, SearchTextEnd, oper, SearchWarnNum.ToString(), encoding);

            DirectoryInfo folder = new DirectoryInfo(SearchPath);
            FileInfo[] allFiles = folder.GetFiles(SearchPattern, SearchOption.AllDirectories);

            List<M_ListView> lv = new List<M_ListView>();

            if (string.IsNullOrWhiteSpace(SearchTextEnd))//只填起始位置
            {
                Parallel.ForEach(allFiles, (file) =>
                {
                    using (StreamReader sr = new StreamReader(file.FullName, Encoding.GetEncoding(encoding), true))
                    {
                        string lineText;//当前读的行
                        int lineNo = 0;
                        while ((lineText = sr.ReadLine()) != null)
                        {
                            lineNo++;
                            if (lineText.IndexOf(SearchTextFirst) >= 0)//符合开始计数的条件
                            {
                                lock (_lock)
                                {
                                    lv.Add(new M_ListView(SearchPath) { File = file.FullName, Line = lineText, LineNo = lineNo });
                                }
                            }
                        }
                        sr.Close();
                    }
                });
            }
            else
            {
                Parallel.ForEach(allFiles, (file) =>
                {
                    using (StreamReader sr = new StreamReader(file.FullName, Encoding.GetEncoding(encoding), true))
                    {
                        int count = 0;
                        bool startCount = false;

                        string lineText;//当前读的行
                        int lineNo = 0;
                        while ((lineText = sr.ReadLine()) != null)
                        {
                            lineNo++;

                            if (startCount == true) count++;//计数

                            if (lineText.IndexOf(SearchTextFirst) >= 0) count = 0; startCount = true;//符合开始计数的条件

                            if (lineText.IndexOf(SearchTextEnd) >= 0 && startCount == true)//符合结束计数的条件
                            {
                                startCount = false;
                                count--;//去掉最后一行的计数
                                if (oper == ">=" && count >= SearchWarnNum)
                                {
                                    lock (_lock)
                                    {
                                        lv.Add(new M_ListView(SearchPath) { Msg = "间隔【" + count + "】行", File = file.FullName, Line = lineText, LineNo = lineNo });
                                    }
                                }
                                else if (oper == "<=" && count <= SearchWarnNum)
                                {
                                    lock (_lock)
                                    {
                                        lv.Add(new M_ListView(SearchPath) { Msg = "间隔【" + count + "】行", File = file.FullName, Line = lineText, LineNo = lineNo });
                                    }
                                }
                            }
                        }
                    }
                });
            }

            var list = lv.OrderBy(s => s.SimplifiedFileName).ThenBy(s => s.LineNo);
            ListView_Result.ItemsSource = list;
            foreach (var item in list)
            {
                _logger.Info("{0} [{1}] : {2}行【{3}】", item.Msg, item.SimplifiedFileName, item.LineNo, item.Line);
            }
            ShowMsg("<<<扫描结束,共找到{0}条符合条件的记录", lv.Count().ToString());
        }

        private void ListView_Result_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var m = (sender as ListView).SelectedItem as M_ListView;

            Clipboard.SetData(DataFormats.Text, m.Line);
            Process.Start(m.File);
        }

        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TextBox_Log_TextChanged(object sender, TextChangedEventArgs e)
        {
            (sender as TextBox).ScrollToEnd();
        }

    }

    public class M_ListView : INotifyPropertyChanged
    {
        private string _path;

        private string _msg;
        private string _file;
        private string _line;
        private int _lineNo;

        public string Msg
        {
            get
            {
                return _msg;
            }

            set
            {
                _msg = value; NotifyPropertyChanged("Msg");
            }
        }
        public string File
        {
            get
            {
                return _file;
            }
            set
            {
                _file = value;
                NotifyPropertyChanged("File");
                NotifyPropertyChanged("SimplifiedFileName");
            }
        }
        public string Line
        {
            get
            {
                return _line;
            }

            set
            {
                _line = value; NotifyPropertyChanged("Line");
            }
        }
        public int LineNo
        {
            get
            {
                return _lineNo;
            }

            set
            {
                _lineNo = value; NotifyPropertyChanged("LineNo");
            }
        }
        public string SimplifiedFileName
        {
            get
            {
                return File.Replace(_path, "...");
            }
        }

        public M_ListView(string path)
        {
            _path = path;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
