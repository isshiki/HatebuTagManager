using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace HatebuTagManager
{
    public class AppUtility
    {
        private static string defaultTextEditorExePath;

        private static string InternalGetDefaultTextEditorExePath()
        {
            // 「HKEY_CLASSES_ROOT\.txt」の既定値（＜.txt値＞）を取得する
            string txtEditorName = "";

            // レジストリ・キーを開く
            string keyDotTxt = ".txt";
            RegistryKey rKeyDotTxt =
              Registry.ClassesRoot.OpenSubKey(keyDotTxt);
            if (rKeyDotTxt != null)
            {

                // レジストリの値を取得する
                string defaultValue =
                  (string)rKeyDotTxt.GetValue(String.Empty);

                // レジストリ・キーを閉じる
                rKeyDotTxt.Close();

                txtEditorName = defaultValue;
            }
            if (txtEditorName == null || txtEditorName == "")
            {
                return "";
            }

            // 「HKEY_CLASSES_ROOT\＜.txt値＞\shell\open\command」
            // の既定値を取得する
            string path = "";

            // レジストリ・キーを開く
            string keyTxtEditor = txtEditorName + @"\shell\open\command";
            RegistryKey rKey =
              Registry.ClassesRoot.OpenSubKey(keyTxtEditor);
            if (rKey != null)
            {

                // レジストリの値を取得する
                string command = (string)rKey.GetValue(String.Empty);

                // レジストリ・キーを閉じる
                rKey.Close();

                if (command == null)
                {
                    return path;
                }

                // 前後の余白を削る
                command = command.Trim();
                if (command.Length == 0)
                {
                    return path;
                }

                // 「"」で始まるパス形式かどうかで処理を分ける
                if (command[0] == '"')
                {
                    // 「"～"」間の文字列を抽出
                    int endIndex = command.IndexOf('"', 1);
                    if (endIndex != -1)
                    {
                        // 抽出開始を「1」ずらす分、長さも「1」引く
                        path = command.Substring(1, endIndex - 1);
                    }
                }
                else
                {
                    // 「（先頭）～（スペース）」間の文字列を抽出
                    int endIndex = command.IndexOf(' ');
                    if (endIndex != -1)
                    {
                        path = command.Substring(0, endIndex);
                    }
                    else
                    {
                        path = command;
                    }
                }
            }

            return path;
        }

        public static bool OpenByTextEditor(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            if (File.Exists(path) == false) return false;
            try
            {
                // 既定のテキスト・エディタのパスを取得する
                if (String.IsNullOrEmpty(defaultTextEditorExePath))
                {
                    defaultTextEditorExePath = InternalGetDefaultTextEditorExePath();
                }

                // 既定のテキスト・エディタを立ち上げる
                Process.Start(defaultTextEditorExePath, path);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
