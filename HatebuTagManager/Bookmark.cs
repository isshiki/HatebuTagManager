using System;
using System.Collections.Generic;

namespace HatebuTagManager
{
    public class Bookmark
    {
        public string Title { get; set; }               // タイトルは取得できるが入力できない。はてブ全体で共通
        public string Url { get; set; }                 // Permalink
        public string CommentWithTags { get; set; }     // タグ込みで100文字まで
        public List<string> Tags { get; set; }          // ブックマークにつけられたタグ。最大10個まで。コメントの先頭に付ける

        public bool PostToTwitter = false;              // 常にfalse
        public bool PostToMixi = false;                 // 常にfalse
        public bool PostToEvernote = false;             // 常にfalse

        public bool PrivateNotPublic { get; set; }      // 非公開でブックマークされたかどうかを表す。ポストする前に取得する
        public DateTime CreatedDatetime { get; set; }   // ブックマークした日時
        public long CreatedEpoch { get; set; }          // ブックマークした日時を表す UNIX epoch time
        public string UserID { get; set; }              // ブックマークしたユーザーのはてな ID
    }
}
