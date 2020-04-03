using AsyncOAuth;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace HatebuTagManager
{
    // [Consumer key を取得して OAuth 開発をはじめよう - Hatena Developer Center](http://developer.hatena.ne.jp/ja/documents/auth/apis/oauth/consumer)
    // OAuth 開発者向け設定ページ：http://www.hatena.ne.jp/oauth/develop
    // 以下のscopeに対して「承認」を有効にしてください
    // read_public     公開情報の読み取り
    // write_public    公開情報の読み取りと書き込み、削除
    // read_private    プライベート情報を含む情報の読み取り
    // write_private   プライベート情報を含む情報の読み取りと書き込み

    public partial class MainWindow : Window
    {
        private const string PRIVATE_KEY_FILE_NAME = "@hatebu.key";
        private const string PRIVATE_KEY_FILE_PATH = "..\\..\\..\\" + PRIVATE_KEY_FILE_NAME;
        private const string TITLE_CONSUMER_KEY = "OAuth Consumer Key:";
        private const string TITLE_CONSUMER_SECRET = "OAuth Consumer Secret:";

        private const string NAME_DATA_FOLDER = "HatebuTagManager";
        private const string NAME_DATA_FILE = "SearchedData.txt";
        private const string NAME_LOG_FILE = "ProcessedLog.txt";


        private static LinearGradientBrush todoBrush = new LinearGradientBrush(
            Color.FromRgb(255, 255, 255), Color.FromRgb(255, 0, 0), new Point(0, 0), new Point(1, 0));
        private static LinearGradientBrush doneBrush = new LinearGradientBrush(
            Color.FromRgb(255, 255, 255), Color.FromRgb(0, 255, 0), new Point(0, 0), new Point(1, 0));
        private static LinearGradientBrush allModeBrush = new LinearGradientBrush(
            Color.FromRgb(255, 255, 255), Color.FromRgb(255, 0, 255), new Point(0, 0), new Point(1, 0));
        private static LinearGradientBrush oneModeBrush = new LinearGradientBrush(
            Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 0), new Point(0, 0), new Point(1, 0));

        private HatebuApiClient apiClient;

        public bool IsProcessing { get; set; }  // 処理を実行中かどうか
        private CancellationTokenSource tokenSource;
        private CancellationToken cancelToken;


        public MainWindow()
        {
            InitializeComponent();

            IsProcessing = false;
            tokenSource = new CancellationTokenSource(); // 非同期処理をキャンセルるるための機能
            cancelToken = tokenSource.Token;             // APIクライアント側に引き渡すトークン

            this.txtblockMessageAuth.Background = todoBrush;
            this.txtblockMessageApi.Background = todoBrush;
            this.txtblockApiStatus.Background = todoBrush;
            this.txtblockApiStatus.Background = todoBrush;
            this.txtblockProcStatus.Background = todoBrush;

            var pathDesktop = System.Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            this.txtboxDataFolderPath.Text = Path.Combine(pathDesktop, NAME_DATA_FOLDER);
        }

        private void btnSaveKeySecret_Click(object sender, RoutedEventArgs e)
        {
            var keyFilePath = Path.GetFullPath(PRIVATE_KEY_FILE_NAME);
            var key = this.txtboxConsumerKey.Text;
            var secret = this.txtboxConsumerSecret.Text;
            File.WriteAllText(keyFilePath, $"OAuth Consumer Key: {key}\r\nOAuth Consumer Secret: {secret}", Encoding.UTF8);
            MessageBox.Show($"KeyとSecretを、\n\n{keyFilePath}\n\nファイルに保存しました！ 次回から自動入力されます。");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var existsKeyFile = false;
            var keyFilePath = Path.GetFullPath(PRIVATE_KEY_FILE_PATH);
            if (File.Exists(keyFilePath))
            {
                existsKeyFile = true;
            }
            else
            {
                keyFilePath = Path.GetFullPath(PRIVATE_KEY_FILE_NAME);
                if (File.Exists(keyFilePath))
                {
                    existsKeyFile = true;
                }
            }
            if (existsKeyFile)
            {
                var lines = File.ReadAllLines(keyFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith(TITLE_CONSUMER_KEY))
                    {
                        this.txtboxConsumerKey.Text = line.Replace(TITLE_CONSUMER_KEY, "").Trim();
                    }
                    else if (line.StartsWith(TITLE_CONSUMER_SECRET))
                    {
                        this.txtboxConsumerSecret.Text = line.Replace(TITLE_CONSUMER_SECRET, "").Trim();
                    }
                }
            }
        }

        private async void BtnRequestPIN_Click(object sender, RoutedEventArgs e)
        {
            if (apiClient != null)
            {
                apiClient = null;
            }

            var consumerKey = this.txtboxConsumerKey.Text.Trim();
            if (String.IsNullOrEmpty(consumerKey))
            {
                MessageBox.Show("Consumer Keyを入力してください。");
                return;
            }

            var consumerSecret = this.txtboxConsumerSecret.Text.Trim();
            if (String.IsNullOrEmpty(consumerSecret))
            {
                MessageBox.Show("Consumer Secretを入力してください。");
                return;
            }

            apiClient = new HatebuApiClient(consumerKey, consumerSecret);
            OAuthUtility.ComputeHash = (key, buffer) => { using (var hmac = new HMACSHA1(key)) { return hmac.ComputeHash(buffer); } };

            var retMessage = await apiClient.RequestPIN();

            if (String.IsNullOrEmpty(retMessage))
            {
                this.txtblockMessageAuth.Background = doneBrush;
                //MessageBox.Show("アプリケーションのアクセス許可を要求しました。");
            }
            else
            {
                this.txtblockMessageAuth.Background = todoBrush;
                MessageBox.Show(retMessage);
            }
        }

        private async void BtnGetAccessToken_Click(object sender, RoutedEventArgs e)
        {
            if (apiClient == null)
            {
                MessageBox.Show("先にPIN番号（アクセス許可のコード）を要求してください。");
                return;
            }

            var pinCode = this.txtboxPIN.Text.Trim();
            if (String.IsNullOrEmpty(pinCode))
            {
                MessageBox.Show("PIN番号（アクセス許可のコード）を入力してください。");
                return;
            }

            var retMessage = await apiClient.GetAccessToken(pinCode);

            if (String.IsNullOrEmpty(retMessage))
            {
                this.txtblockMessageApi.Text = "以上で認証は完了です。【 ステップ 2 】に進んでください。";
                this.txtblockMessageApi.Background = doneBrush;
                //MessageBox.Show("アクセストークンの取得が完了しました。");
            }
            else
            {
                this.txtblockMessageApi.Text = "まだログイン認証が済んでいません。まずは上記を上から順に実行してください。";
                this.txtblockMessageApi.Background = todoBrush;
                MessageBox.Show(retMessage);
            }
        }

        private async void BtnGetAllBookmaks_Click(object sender, RoutedEventArgs e)
        {
            string dataFolderPath = GetDataDirectory();
            if (dataFolderPath == null) return;


            this.txtblockApiStatus.Text = "★★★すべてのブックマークを取得しています。お待ちください。★★★";
            this.txtblockApiStatus.Background = allModeBrush;

            var info = await apiClient.GetMyAllBookmarks();
            if (String.IsNullOrEmpty(info))
            {
                if (apiClient.LastError != null)
                {
                    MessageBox.Show($"{apiClient.LastErrTitle}\n\n【内容】{apiClient.LastError.Message}");
                }
            }
            else
            {
                this.txtblockApiStatus.Text = "すべてのブックマークを取得しました。";
                this.txtblockApiStatus.Background = doneBrush;

                var dataFilePath = Path.Combine(dataFolderPath, NAME_DATA_FILE);
                File.WriteAllText(dataFilePath, info, Encoding.UTF8);
                this.txtboxGotResult.Text = $"情報量が多すぎる場合があるため別ファイルに保存しました。\n{dataFilePath}\nを参照してください。";
                if (this.chckboxOpenDataFolder.IsChecked == true)
                {
                    AppUtility.OpenByTextEditor(dataFilePath);
                }
                //MessageBox.Show("成功！");
            }
        }

        private async void BtnGetOneBookmak_Click(object sender, RoutedEventArgs e)
        {
            this.txtblockApiStatus.Text = "★★★1つのブックマークを取得しています。お待ちください。★★★";
            this.txtblockApiStatus.Background = oneModeBrush;


            var url = this.txtboxOneUrl.Text;
            var info = await apiClient.GetMyOneBookmark(url);
            if (String.IsNullOrEmpty(info))
            {
                if (apiClient.LastError != null)
                {
                    MessageBox.Show($"{apiClient.LastErrTitle}\n\n【内容】{apiClient.LastError.Message}");
                }
            }
            else
            {
                this.txtblockApiStatus.Text = "1つのブックマークを取得しました。";
                this.txtblockApiStatus.Background = doneBrush;
                var result = Regex.Unescape(info);
                this.txtboxGotResult.Text = result;
                btnGetOneToAllBookmark.IsEnabled = true;
                //MessageBox.Show("成功！");
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string dataFolderPath = GetDataDirectory();
            if (dataFolderPath == null) dataFolderPath = "";

            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog(); ;
            folderBrowserDialog1.SelectedPath = dataFolderPath;
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtboxDataFolderPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private string GetDataDirectory()
        {
            var dataFolderPath = this.txtboxDataFolderPath.Text;
            try
            {
                if (Directory.Exists(dataFolderPath) == false)
                {
                    Directory.CreateDirectory(dataFolderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"指定のディレクトリ： \n\n{dataFolderPath}\n\nでのディレクトリ作成に失敗しました。\n\n{ex.Message}");
                return null;
            }

            return dataFolderPath;
        }

        private void BtnGetOneToAllBookmark_Click(object sender, RoutedEventArgs e)
        {
            apiClient.ResetOneBookmark();
            if (apiClient.modeAllBookmarks())
            {
                this.txtblockApiStatus.Text = "取得済みのすべてのブックマークを使います。";
                this.txtblockApiStatus.Background = allModeBrush;
                var result = Regex.Unescape(apiClient.GetAllBookmarksJson());
                this.txtboxGotResult.Text = result;

            }
            else
            {
                this.txtblockApiStatus.Text = "先に、すべてのブックマークを取得してください。";
                this.txtblockApiStatus.Background = null;
                this.txtboxGotResult.Text = String.Empty;
            }
            btnGetOneToAllBookmark.IsEnabled = false;
        }

        private async void buttonChangeTag_Click(object sender, RoutedEventArgs e)
        {
            var fromTagName = this.textboxFromTagName.Text;
            if (String.IsNullOrEmpty(fromTagName))
            {
                MessageBox.Show("［変換「前」のタグ］名を入力してください。");
                return;
            }
            var toTagName = this.textboxToTagName.Text;
            if (String.IsNullOrEmpty(toTagName))
            {
                if (MessageBox.Show($"［変換「後」のタグ］名が空なので、タグが削除されます。OKでしょうか？",
                    "タグの削除", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Cancel) return;
            }

            var timeString = apiClient.EstimateTimeForChangingTag(fromTagName);
            if (PrepareProc(timeString, "タグの更新") == false) return;

            var info = await apiClient.ChangeTagName(fromTagName, toTagName, this.txtblockProcStatus, cancelToken);

            FinalizeProc(info);
        }

        private async void buttonDeleteBookmarkByTag_Click(object sender, RoutedEventArgs e)
        {
            var delTagName = this.textboxDeleteTagName.Text;
            if (String.IsNullOrEmpty(delTagName))
            {
                MessageBox.Show("［削除対象のタグ］名を入力してください。");
                return;
            }

            var timeString = apiClient.EstimateTimeForChangingTag(delTagName);
            if (PrepareProc(timeString, "タグによる削除") == false) return;

            var info = await apiClient.DeleteBookmarksByTagName(delTagName, this.txtblockProcStatus, cancelToken);

            FinalizeProc(info);
        }

        private async void buttonDeleteBookmarkByOlderDate_Click(object sender, RoutedEventArgs e)
        {
            var olderDate = this.datepickerOlderDate.SelectedDate;
            if ((olderDate == null) || (olderDate < new DateTime(1990, 1, 1)))
            {
                MessageBox.Show("［削除対象の日付］を入力してください。");
                return;
            }

            var timeString = apiClient.EstimateTimeForAllItems();
            if (PrepareProc(timeString, "日付による削除") == false) return;

            var info = await apiClient.DeleteBookmarksByOlderDate((DateTime)olderDate, this.txtblockProcStatus, cancelToken);

            FinalizeProc(info);
        }

        private bool PrepareProc(string timeString, string procName)
        {
            var retUserChoice = MessageBox.Show($"※この処理を実行するとやり直しできないので慎重に選んでください。\n\n" +
                $"処理完了までにかかる予測時間は「{timeString}」です。\n\n進めてもよいでしょうか？",
                procName, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (retUserChoice == MessageBoxResult.Cancel) return false;
            retUserChoice = MessageBox.Show($"念のため、もう一回聞きます！ このまま進めてもよいでしょうか？",
                $"{procName}【再確認】", MessageBoxButton.OKCancel, MessageBoxImage.Asterisk, MessageBoxResult.Cancel);
            if (retUserChoice == MessageBoxResult.Cancel) return false;

            this.txtblockProcStatus.Text = $"★★★{procName}処理を実行しています。お待ちください。★★★";
            this.txtblockProcStatus.Background = oneModeBrush;

            IsProcessing = true;

            return true;
        }

        private string GetLogFileNmae()
        {
            var dataFolderPath = this.txtboxDataFolderPath.Text;
            var nowString = DateTime.Now.ToString("yyyyMMddHHmmss");
            var dataFilePath = Path.Combine(dataFolderPath, nowString + NAME_LOG_FILE);
            return dataFilePath;
        }

        private void FinalizeProc(string info)
        {
            string dataFilePath = GetLogFileNmae();
            File.WriteAllText(dataFilePath, info, Encoding.UTF8);
            this.txtboxProcResult.Text = $"処理結果はファイルに保存しました。\n{dataFilePath}\nを参照してください。";
            if (this.chckboxOpenDataFolder.IsChecked == true)
            {
                AppUtility.OpenByTextEditor(dataFilePath);
            }
            this.txtblockProcStatus.Text = "処理を完了しました。【ステップ 3 】の処理を行うには、もう一度【ステップ 2 】を実施してください。";
            this.txtblockProcStatus.Background = doneBrush;
            //MessageBox.Show("成功！");

            apiClient.ResetBookmarks();
            apiClient.ResetOneBookmark();
            this.txtblockApiStatus.Text = $"［実行状況はここに表示されます］";
            this.txtblockApiStatus.Background = todoBrush;
            
            IsProcessing = false;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsProcessing == false) return; // 処理実行中でなければ素直に閉じる

            if (MessageBox.Show("処理中ですが、本アプリ「HatebuTagManager」を強制終了します。\nよろしいですか？\n\n（強制終了前に、ログ出力処理が実行されます。）", "強制終了の確認", 
                MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK)
            {
                e.Cancel = true;
                await CancellationWhileProcessing();
                return;
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }

        private async Task CancellationWhileProcessing()
        {
            // 非同期処理を処理をキャンセル
            tokenSource.Cancel();

            this.txtblockProcStatus.Background = todoBrush;
            int counter = 0;
            while (true)
            {
                counter++;
                if (IsProcessing == false)
                {
                    this.Close();
                    return; // 処理実行中でなければ閉じる
                }
                this.txtblockProcStatus.Text = $"「キャンセル中」です。複数の残処理の完了まで少しお待ちください。【処理中カウンター：{counter}】";
                await Task.Delay(100);
            }
        }

    }
}

