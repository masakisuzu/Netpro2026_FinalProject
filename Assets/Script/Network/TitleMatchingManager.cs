using System.Collections.Generic;
using Fusion;
using R3;
using UnityEngine;

namespace Script.Network
{
    /// <summary>
    /// シーンをまたがないTitleシーン限定のマッチング管理
    /// 参加者リスト・Ready管理・全員揃った後のシーン遷移トリガー等
    /// </summary>
    public class TitleMatchingManager : MonoBehaviour
    {
        public static TitleMatchingManager Instance { get; private set; }
        private readonly ReactiveProperty<List<TitlePlayer>> _players = new(new List<TitlePlayer>());
        public ReadOnlyReactiveProperty<List<TitlePlayer>> Players => _players;

        private void Awake()
        {
            Instance = this;
        }
        
        /// <summary>
        /// Readyボタンから呼ばれる。自分自身のNetworkPlayerのIsReadyをtrueにする
        /// 参加者リストの中から、自分自身が所有している NetworkPlayer を探す
        /// （Spawnの際に権限を与えて自分のものだと明示した NetworkPlayer のこと）
        /// </summary>
        public void SetLocalPlayerReady()
        {
            foreach (var player in _players.CurrentValue)
            {
                if (player.Object.HasStateAuthority) 
                    player.SetReady();
            }
        }
        
        /// <summary>
        /// 全員の IsReady を確認して true なっていたら
        /// 代表者（マスタークライアント）だけがゲーム開始（シーン遷移）を実行する
        /// </summary>
        public void CheckAllReadyAndStartGame()
        {
            var players = _players.CurrentValue;

            // TODO
            // 2人以下は成り立たないので判定しない
            if (players.Count <= 0) return;

            // 1人でも未Readyがいたら何もしない
            foreach (var player in players)
            {
                if (!player.IsReady) return;
            }

            // 全員Readyだった場合でも、実行するのは代表者1人だけにする
            // （全クライアントで同時にLoadSceneが呼ばれるのを防ぐため）
            var runner = JankenNetworkManager.Instance.Runner;
            if (runner.IsSharedModeMasterClient)
            {
                runner.LoadScene(SceneRef.FromPath("Assets/Scenes/InGame.unity"));
            }
        }
        
        // -------------------- 参加者リストの更新系（R3発火でViewも変わる） --------------------
        
        /// <summary>
        /// 自分が参加した時に呼ぶ
        /// NetworkPlayerがSpawnされた時に呼ばれ、参加者リストに追加する
        /// </summary>
        public void RegisterPlayer(TitlePlayer player)
        {
            // 既存のリストの中身を全部コピーして、新しいリストを再生成
            var list = new List<TitlePlayer>(_players.CurrentValue);
    
            // 新しく作ったリストにplayerを追加する
            list.Add(player);
    
            // .Valueに代入してR3発火
            _players.Value = list;
            
            // この書き方だと通知いかないらしい。だから新しいリスト丸ごとを用意する必要があった
            // _players.CurrentValue.Add(player);
        }

        /// <summary>
        /// 誰かが退出した時に呼ぶ
        /// NetworkPlayerがDespawnされた時に呼ばれ、参加者リストから除外する
        /// </summary>
        public void UnregisterPlayer(TitlePlayer player)
        {
            // RegisterPlayer と同じ手順で今度は Remove
            var list = new List<TitlePlayer>(_players.CurrentValue);
            list.Remove(player);
            _players.Value = list;
        }

        /// <summary>
        /// 他が参加してきたときに呼ぶ
        /// リスト自体の中身（Networked値）が後から変わった時に、表示側へ再通知する用
        /// </summary>
        public void RefreshPlayerList()
        {
            // 今のリストをそのまま再代入
            _players.Value = new List<TitlePlayer>(_players.CurrentValue);
        }
    }
}