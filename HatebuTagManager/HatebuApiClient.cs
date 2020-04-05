using AsyncOAuth;
using Codeplex.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HatebuTagManager
{
    public class HatebuApiClient
    {
        #region 数値関連の定数定義
        private const int WAIT_SEC_PER_CALL = 3;
        private const int PROC_SEC_PER_CALL = 2;
        private const string API_VERSION = "1";
        #endregion

        #region はてなブックマークAPIのOAuth関連オブジェクト
        private readonly string oAuthConsumerKey;
        private readonly string oAuthConsumerSecret;
        private OAuthAuthorizer oAuthAuthorizer;
        private RequestToken oAuthRequestToken;
        private AccessToken oAuthAccessToken;
        #endregion

        private HttpClient client;   // HTTP処理クライアント

        #region ブックマーク保存オブジェクト
        private List<Bookmark> listBookmarks = new List<Bookmark>();
        private List<Bookmark> oneBookmark = new List<Bookmark>();
        private string jsonAllBookmarks = String.Empty;
        #endregion

        #region エラー情報関連
        public Exception LastError;
        public string LastErrTitle;
        public void ResetLastError()
        {
            // 初期化
            LastErrTitle = "";
            LastError = null;
        }
        #endregion

        public string CurrentUserName { get; set; }  // 処理実行中のはてな「ユーザーID」

        #region 初期化処理

        private HatebuApiClient()
        {
            // 引数なしのコンストラクターは許容しない
        }

        public HatebuApiClient(string consumerKey, string consumerSecret)
        {
            CurrentUserName = "<user-name>";
            this.oAuthConsumerKey = consumerKey;
            this.oAuthConsumerSecret = consumerSecret;
        }

        #endregion

        #region 【ステップ1】ログイン認証

        public async Task<string> RequestPIN()
        {
            var retMessage = String.Empty;
            try
            {
                oAuthAuthorizer = new OAuthAuthorizer(this.oAuthConsumerKey, this.oAuthConsumerSecret);

                var tokenResponse = await oAuthAuthorizer.GetRequestToken(
                    "https://www.hatena.com/oauth/initiate",
                    new[] { new KeyValuePair<string, string>("oauth_callback", "oob") },
                    new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("scope", "read_public,write_public,read_private,write_private") }));

                oAuthRequestToken = tokenResponse.Token;

            }
            catch (Exception ex)
            {
                retMessage = $"RequestPINでエラーが発生しました。【内容】{ex.Message}";
            }

            if (String.IsNullOrEmpty(retMessage))
            {
                var pinRequestUrl = oAuthAuthorizer.BuildAuthorizeUrl("https://www.hatena.ne.jp/oauth/authorize", oAuthRequestToken);

                try
                {
                    Process.Start(pinRequestUrl);
                }
                catch (Exception ex)
                {
                    retMessage = $"Webページ「{pinRequestUrl}」を開くのに失敗しました。手動で開いてください。【内容】{ex.Message}";
                }
            }

            return retMessage;
        }

        public async Task<string> GetAccessToken(string pinCode)
        {
            var retMessage = String.Empty;
            try
            {
                var accessTokenResponse = await oAuthAuthorizer.GetAccessToken("https://www.hatena.com/oauth/token", oAuthRequestToken, pinCode);
                oAuthAccessToken = accessTokenResponse.Token;
            }
            catch (Exception ex)
            {
                retMessage = $"GetAccessTokenでエラーが発生しました。【内容】{ex.Message}";
            }

            return retMessage;
        }

        void CreateHttpClient()
        {
            if (client != null) return;

            client= OAuthUtility.CreateOAuthClient(oAuthConsumerKey, oAuthConsumerSecret, oAuthAccessToken);
            var userAgent = $"HatebuTagManager made by misshiki using by {Environment.MachineName}-{Environment.UserName}";
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            // dispose不要
        }

        #endregion

        #region 【ステップ2】全ブックマークデータの取得

        public async Task<string> GetMyAllBookmarks()
        {
            ResetBookmarks();
            ResetOneBookmark();

            CreateHttpClient();

            //var myjson = await client.GetStringAsync("http://n.hatena.com/applications/my.json");  // アプリケーション情報
            // {"profile_image_url":"https://cdn.profile-image.st-hatena.com/users/{user-id}/profile.gif","url_name":"{user-id}","display_name":"{表示名}"}

            var jsonUserInfo = String.Empty;
            try
            {
                jsonUserInfo = await client.GetStringAsync($"https://bookmark.hatenaapis.com/rest/{API_VERSION}/my"); // ユーザー情報
            }
            catch (Exception ex)
            {
                LastErrTitle = "GetMyAllBookmarks-GetStringAsyncでエラーが発生しました。";
                LastError = ex;
                return String.Empty;
            }

            var userName = String.Empty;
            try
            {
                // {"is_oauth_evernote":false,"is_oauth_twitter":true,"name":"{user-id}","plususer":false,"is_oauth_facebook":false,"is_oauth_mixi_check":false,"private":false}
                var dynamicUserInfo = DynamicJson.Parse(jsonUserInfo);
                userName = dynamicUserInfo.name;  // "{user-id}";
            }
            catch (Exception ex)
            {
                LastErrTitle = "GetMyAllBookmarks-DynamicJsonParseでエラーが発生しました。";
                LastError = ex;
                return String.Empty;
            }
            CurrentUserName = userName;

            var textAllData = String.Empty;
            try
            {
                textAllData = await client.GetStringAsync($"https://b.hatena.ne.jp/{userName}/search.data"); // データ
            }
            catch (Exception ex)
            {
                LastErrTitle = "GetMyAllBookmarks-search.dataでエラーが発生しました。";
                LastError = ex;
                return String.Empty;
            }

            SaveAllBookmarks(textAllData, listBookmarks);
            jsonAllBookmarks = textAllData;

            return textAllData;
        }

        private static void SaveAllBookmarks(string textAllData, List<Bookmark> bookmarkList)
        {
            var linesAllData = textAllData.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var linesLength = linesAllData.Length;
            var bookmarksLength = linesLength / 3;
            for (var i = 0; i < bookmarksLength; i++)
            {
                var indexTitle = i * 3;
                var indexComment = indexTitle + 1;
                var indexURL = indexTitle + 2;

                var title = linesAllData[indexTitle];
                var url = linesAllData[indexURL];
                var comment = linesAllData[indexComment];
                //タイトル
                //[タグ名]コメント
                //https://blog.masahiko.info/entry/2020/04/03/184745

                if (url.StartsWith("http") == false) break;

                var matches = Regex.Matches(comment, "\\[([^\\]]*?)\\]");
                var tagList = matches.Cast<Match>().Select(match => match.Value).ToList();

                var bkmark = new Bookmark();
                bkmark.Title = title;
                bkmark.Url = url;
                bkmark.CommentWithTags = comment;  // タグ込みで100文字まで
                bkmark.Tags = tagList;  // 最大10個まで。コメントの先頭に付ける
                //※日付情報が含まれていない...
                bookmarkList.Add(bkmark);
            }
        }

        #endregion

        #region 【ステップ2】における、1つのブックマークの取得

        public async Task<string> GetMyOneBookmark(string url)
        {
            ResetOneBookmark();

            var urlEnc = Uri.EscapeDataString(url);

            CreateHttpClient();

            var json = String.Empty;
            try
            {
                json = await client.GetStringAsync($"https://bookmark.hatenaapis.com/rest/{API_VERSION}/my/bookmark?url={urlEnc}");
            }
            catch (Exception ex)
            {
                LastErrTitle = "GetMyOneBookmarkでエラーが発生しました。";
                LastError = ex;
                return String.Empty;
            }

            SaveOneBookmark(json, url, oneBookmark);

            return json;
        }

        private static void SaveOneBookmark(string json, string url, List<Bookmark> bookmarkList)
        {
            if (bookmarkList == null) return;
            var dynamicBookmarkInfo = DynamicJson.Parse(json);
            var bkmark = new Bookmark();
            bkmark.Title = "＜未取得＞";
            bkmark.Url = url;
            bkmark.CommentWithTags = dynamicBookmarkInfo.comment_raw;  // タグ込みで100文字まで
            bkmark.Tags = ((string[])dynamicBookmarkInfo.tags).ToList<String>();  // 最大10個まで。コメントの先頭に付ける
            bkmark.CreatedDatetime = DateTime.Parse(dynamicBookmarkInfo.created_datetime, null, System.Globalization.DateTimeStyles.RoundtripKind);
            bookmarkList.Add(bkmark);
        }

        #endregion

        #region 【ステップ3】タグの一括更新

        public async Task<string> ChangeTagName(string fromTagName, string toTagName, System.Windows.Controls.TextBlock txtblockProcStatus, CancellationToken cancelToken)
        {
            var fromTag = $"[{fromTagName}]";
            var toTag = String.IsNullOrEmpty(toTagName) ? String.Empty : $"[{toTagName}]";

            var targetBookmarks = GetTargetBookmarks();
            if (targetBookmarks == null) return String.Empty;

            var sbResult = new StringBuilder();
            sbResult.Append($"タグ名を {fromTag} から {toTag} に変更したブックマークのURL一覧：\r\n");

            CreateHttpClient();

            var doneUrls = new List<string>();
            var errorUrls = new List<string>();

            var targetBkmkList = targetBookmarks.Where(x => x.Tags.Contains(fromTag));
            var counter = 0;
            var allnum = targetBkmkList.Count();
            foreach (var bookmarkItem in targetBkmkList)
            {
                counter++;
                await Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => txtblockProcStatus.Text = $"{counter}/{allnum} {bookmarkItem.Url}"));

                if (doneUrls.Contains(bookmarkItem.Url)) continue; // 同じURLが含まれているケースがある

                var found = false;
                var sbTags = new StringBuilder();
                foreach (var tagName in bookmarkItem.Tags)
                {
                    if (tagName == fromTag)
                    {
                        if (String.IsNullOrEmpty(toTagName) == false)
                        {
                            sbTags.Append(toTag);
                        }
                        found = true;
                    }
                    else
                    {
                        sbTags.Append(tagName);
                    }
                }
                if (found == false) continue;

                var tagsText = sbTags.ToString();

                var re = new Regex("\\[.*?\\]", RegexOptions.Singleline);
                var commentText = re.Replace(bookmarkItem.CommentWithTags, "");
                var encodedCmnt = Uri.EscapeDataString(tagsText + commentText);

                //var parameters = new Dictionary<string, string>()
                //{
                //    { "url", bookmarkItem.Url },
                //    { "comment", tagsText + commentText },
                //    //{ "tags", tagsText },
                //    //{ "post_twitter", bookmarkItem.PostToTwitter ? "true" : "false" },
                //    //{ "post_mixi", bookmarkItem.PostToMixi ? "true" : "false" },
                //    //{ "post_evernote", bookmarkItem.PostToEvernote ? "true" : "false" },
                //    //{ "private", bookmarkItem.PrivateNotPublic ? "true" : "false" }
                //};
                //var content = new FormUrlEncodedContent(parameters);

                var hatebuUrl = Uri.EscapeDataString(bookmarkItem.Url);
                try
                {
                    var response = await client.PostAsync($"https://bookmark.hatenaapis.com/rest/{API_VERSION}/my/bookmark" +
                        $"?url={hatebuUrl}" +
                        $"&comment={encodedCmnt}", // コンテンツのポストではなく、クエリパラメーターらしい...
                        //$"&tags={tagsText}" +    // 上のコメントに含めておけば指定不要（どう表現すればいいの？ "[タグ名1],[タグ名2]" のような形かな）
                        //$"&private={(bookmarkItem.PrivateNotPublic ? "true" : "false")}", // 「公開｜非公開」は切り替えない
                        null);                     //content); // クエリパラメーターで指定するのでコンテンツは含めていない。
                    var resultJson = await response.Content.ReadAsStringAsync();
                    if (resultJson.IndexOf("comment_raw") != -1)
                    {
                        sbResult.Append($"{RenameToHatebu(bookmarkItem)}\r\n");
                        doneUrls.Add(bookmarkItem.Url);
                    }
                    else
                    {
                        if (errorUrls.Contains(bookmarkItem.Url) == false)
                        {
                            errorUrls.Add(bookmarkItem.Url);
                            var errUrl = RenameToSearch(bookmarkItem, CurrentUserName);
                            sbResult.Append($"【エラー: {resultJson}】( {bookmarkItem.Url} ) {errUrl}\r\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sbResult.Append($"【エラー: APIによるタグ更新処理中】{bookmarkItem.Url}［← 手動で削除してください］{ex.Message}\r\n");
                    // 続行する
                }

                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
                await Task.Delay(WAIT_SEC_PER_CALL * 1000);
                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
            }
            return sbResult.ToString();
        }

        #endregion

        #region 【ステップ3】タグによる、複数ブックマークの一括削除

        public async Task<string> DeleteBookmarksByTagName(string targetTagName, System.Windows.Controls.TextBlock txtblockProcStatus, CancellationToken cancelToken)
        {
            var targetTag = $"[{targetTagName}]";

            var sbResult = new StringBuilder();
            sbResult.Append($"タグ名が「{targetTagName}」だったので削除したブックマークのURL一覧：\r\n");

            CreateHttpClient();

            var targetBookmarks = GetTargetBookmarks();
            if (targetBookmarks == null) return String.Empty;

            var doneUrls = new List<string>();
            var errorUrls = new List<string>();

            var targetBkmkList = targetBookmarks.Where(x => x.Tags.Contains(targetTag));
            var counter = 0;
            var allnum = targetBkmkList.Count();
            foreach (var bookmarkItem in targetBkmkList)
            {
                counter++;
                await Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => txtblockProcStatus.Text = $"{counter}/{allnum} {bookmarkItem.Url}"));

                if (doneUrls.Contains(bookmarkItem.Url)) continue; // 同じURLが含まれているケースがある

                var deleteMode = false;
                foreach (var tagName in bookmarkItem.Tags)
                {
                    if (tagName == targetTag)
                    {
                        deleteMode = true;
                        break;
                    }
                }
                if (deleteMode == false) continue;

                var hatebuUrl = Uri.EscapeDataString(bookmarkItem.Url);
                try
                {
                    var response = await client.DeleteAsync($"https://bookmark.hatenaapis.com/rest/{API_VERSION}/my/bookmark?url={hatebuUrl}");

                    var resultJson = await response.Content.ReadAsStringAsync();
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.NoContent:
                        case System.Net.HttpStatusCode.NotFound:
                            sbResult.Append($"{RenameToHatebu(bookmarkItem)}\r\n");
                            doneUrls.Add(bookmarkItem.Url);
                            break;
                        default:
                            if (errorUrls.Contains(bookmarkItem.Url) == false)
                            {
                                errorUrls.Add(bookmarkItem.Url);
                                var errUrl = RenameToSearch(bookmarkItem, CurrentUserName);
                                sbResult.Append($"【エラー: {resultJson}】( {bookmarkItem.Url} ) {errUrl}\r\n");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    sbResult.Append($"【エラー: APIによるタグ指定削除処理中】{bookmarkItem.Url}［← 手動で削除してください］{ex.Message}\r\n");
                    // 続行する
                }

                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
                await Task.Delay(WAIT_SEC_PER_CALL * 1000);
                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
            }

            return sbResult.ToString();
        }

        #endregion

        #region 【ステップ3】日付による、複数ブックマークの一括削除

        public async Task<string> DeleteBookmarksByOlderDate(DateTime olderThanThisDate, System.Windows.Controls.TextBlock txtblockProcStatus, CancellationToken cancelToken)
        {
            var sbResult = new StringBuilder();
            sbResult.Append($"日付が「{olderThanThisDate.ToString()}」よりも古かったので削除したブックマークのURL一覧：\r\n");

            CreateHttpClient();

            var targetBookmarks = GetTargetBookmarks();
            if (targetBookmarks == null) return String.Empty;

            var doneUrls = new List<string>();
            var errorUrls = new List<string>();

            var counter = 0;
            var allnum = targetBookmarks.Count();
            foreach (var bookmarkItem in targetBookmarks.AsEnumerable().Reverse())
            {
                counter++;
                await Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => txtblockProcStatus.Text = $"{counter}/{allnum} {bookmarkItem.Url}"));

                if (doneUrls.Contains(bookmarkItem.Url)) continue; // 同じURLが含まれているケースがある

                var createdDate = bookmarkItem.CreatedDatetime;
                if ((createdDate == null) || (createdDate.Year < 1990))
                {
                    var json = await GetMyOneBookmark(bookmarkItem.Url);
                    if (String.IsNullOrEmpty(json) || (oneBookmark.Count() <= 0))
                    {
                        if (errorUrls.Contains(bookmarkItem.Url) == false)
                        {
                            errorUrls.Add(bookmarkItem.Url);
                            var errUrl = RenameToSearch(bookmarkItem, CurrentUserName);
                            sbResult.Append($"【エラー: APIによる情報取得不可】( {bookmarkItem.Url} )  {errUrl} ［← 手動で削除してください］\r\n");
                        }
                        // 続行する
                        continue;
                    }
                    createdDate = oneBookmark[0].CreatedDatetime;

                    if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
                    await Task.Delay(WAIT_SEC_PER_CALL * 1000);
                    if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
                }

                var deleteMode = (createdDate < olderThanThisDate);
                if (deleteMode == false) break; // 削除するものがないなら、今後のブックマークはより後日のものなので、今後もない

                await Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => txtblockProcStatus.Text = $"{counter}/{allnum} [{createdDate}] {bookmarkItem.Url}"));

                var hatebuUrl = Uri.EscapeDataString(bookmarkItem.Url);
                try
                {
                    var response = await client.DeleteAsync($"https://bookmark.hatenaapis.com/rest/{API_VERSION}/my/bookmark?url={hatebuUrl}");

                    var resultJson = await response.Content.ReadAsStringAsync();
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.NoContent:
                        case System.Net.HttpStatusCode.NotFound:
                            sbResult.Append($"{RenameToHatebu(bookmarkItem)}\r\n");
                            doneUrls.Add(bookmarkItem.Url);
                            break;
                        default:
                            if (errorUrls.Contains(bookmarkItem.Url) == false)
                            {
                                errorUrls.Add(bookmarkItem.Url);
                                var errUrl = RenameToSearch(bookmarkItem, CurrentUserName);
                                sbResult.Append($"【エラー: {resultJson}】( {bookmarkItem.Url} ) {errUrl}\r\n");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    sbResult.Append($"【エラー: APIによる日付指定削除処理中】{bookmarkItem.Url}［← 手動で削除してください］{ex.Message}\r\n");
                    // 続行する
                }

                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
                await Task.Delay(WAIT_SEC_PER_CALL * 1000);
                if (cancelToken.IsCancellationRequested) return sbResult.ToString(); // 非同期のキャンセル指定を受け取る
            }

            return sbResult.ToString();
        }

        #endregion

        #region 【ステップ3】における、ブックマークモード（すべて／1つ）の判定／初期化と、それに基づくブックマーク取得

        public bool modeOneBookmark()
        {
            if ((oneBookmark == null) || (oneBookmark.Count() <= 0))
            {
                return false;
            }
            return true;
        }


        public bool modeAllBookmarks()
        {
            if (modeOneBookmark())
            {
                return false;
            }
            if ((listBookmarks == null) || (listBookmarks.Count() <= 0))
            {
                return false;
            }
            return true;
        }

        public void ResetBookmarks()
        {
            listBookmarks.Clear();
            jsonAllBookmarks = String.Empty;
        }


        public void ResetOneBookmark()
        {
            oneBookmark.Clear();
        }

        private List<Bookmark> GetTargetBookmarks()
        {
            List<Bookmark> targetBookmarks = null;
            if ((oneBookmark != null) && (oneBookmark.Count() > 0))
            {
                targetBookmarks = oneBookmark;
            }
            else if ((listBookmarks == null) || (listBookmarks.Count() <= 0))
            {
                LastErrTitle = "先に、すべてのブックマーク、もしくは1つのブックマークを取得してください。";
                LastError = new Exception("【ステップ2】を実行してください。"); //(new StackTrace(true).ToString());
                return null;
            }
            else
            {
                targetBookmarks = listBookmarks;
            }
            return targetBookmarks;
        }

        public string GetAllBookmarksJson()
        {
            return jsonAllBookmarks;
        }

        #endregion

        #region 【ステップ3】における、予測実行時間の計算機能

        public string EstimateTimeForChangingTag(string tagName)
        {
            var targetBookmarks = GetTargetBookmarks();
            if (targetBookmarks == null) return String.Empty;
            int count = targetBookmarks.Count(x => x.Tags.Contains($"[{tagName}]"));
            int secPerCall = WAIT_SEC_PER_CALL + PROC_SEC_PER_CALL;
            int totalSeconds = count * secPerCall;
            var TotalTimeSpan = new TimeSpan(0, 0, totalSeconds);
            var timespanString = TotalTimeSpan.ToString("hh'時'mm'分'ss'秒間'");
            return timespanString;
        }

        public string EstimateTimeForAllItems()
        {
            var targetBookmarks = GetTargetBookmarks();
            if (targetBookmarks == null) return String.Empty;
            int count = targetBookmarks.Count();
            int secPerCall = WAIT_SEC_PER_CALL + PROC_SEC_PER_CALL;
            int totalSeconds = count * secPerCall;
            var TotalTimeSpan = new TimeSpan(0, 0, totalSeconds);
            var timespanString = TotalTimeSpan.ToString("'最長で'hh'時'mm'分'ss'秒間'");
            return timespanString;
        }

        #endregion

        #region 【ステップ3】における、ログ保存用のはてぶURLの取得機能

        private static string RenameToHatebu(Bookmark bookmarkItem)
        {
            return bookmarkItem.Url.Replace("http://", "＠＠＠").Replace("https://", "＠＠＠").Replace("＠＠＠", "https://b.hatena.ne.jp/entry/s/");
        }

        private static string RenameToSearch(Bookmark bookmarkItem, string userID)
        {
            var encTitle = UrlHatebuEncode(bookmarkItem.Title);
            return $"https://b.hatena.ne.jp/{userID}/search?q={encTitle}";
        }

        public static string UrlHatebuEncode(string stringToEscape)
        {
            var sbEncoded = new StringBuilder();
            foreach (var chr in stringToEscape)
            {
                switch (chr)
                {
                    case '!': sbEncoded.Append("%21"); continue;
                    case '(': sbEncoded.Append("%28"); continue;
                    case ')': sbEncoded.Append("%29"); continue;
                    case '_': sbEncoded.Append("_"); continue;
                    case '-': sbEncoded.Append("-"); continue;
                    case '*': sbEncoded.Append("*"); continue;
                    case '.': sbEncoded.Append("."); continue;
                    case ' ': sbEncoded.Append("+"); continue;
                    case '?': sbEncoded.Append("%3F"); continue;
                    case '#': sbEncoded.Append("%23"); continue;
                    case '$': sbEncoded.Append("%24"); continue;
                    case '%': sbEncoded.Append("%25"); continue;
                    case '&': sbEncoded.Append("%26"); continue;
                    case '|': sbEncoded.Append("%7C"); continue;
                    case '@': sbEncoded.Append("%40"); continue;
                    case '\\': sbEncoded.Append("%5C"); continue;
                    case '/': sbEncoded.Append("%2F"); continue;
                    case '[': sbEncoded.Append("%5B"); continue;
                    case ']': sbEncoded.Append("%5D"); continue;
                    case '{': sbEncoded.Append("%7B"); continue;
                    case '}': sbEncoded.Append("%7D"); continue;
                    case '<': sbEncoded.Append("%3C"); continue;
                    case '>': sbEncoded.Append("%3E"); continue;
                    case '+': sbEncoded.Append("%2B"); continue;
                    case '=': sbEncoded.Append("%3D"); continue;
                    case '^': sbEncoded.Append("%5E"); continue;
                    case '~': sbEncoded.Append("%7E"); continue;
                    case '"': sbEncoded.Append("%22"); continue;
                    case '\'': sbEncoded.Append("%27"); continue;
                    case '`': sbEncoded.Append("%60"); continue;
                    case ';': sbEncoded.Append("%3B"); continue;
                    case ':': sbEncoded.Append("%3A"); continue;
                    case ',': sbEncoded.Append("%2C"); continue;
                }

                var chrString = Uri.EscapeUriString(chr.ToString());
                sbEncoded.Append(chrString);
            }

            return sbEncoded.ToString();
        }

        #endregion

    }
}
