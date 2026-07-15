using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using R3;
using Script.Network.Player;
using UnityEngine;

namespace Script.Network.Manager
{
    /// <summary>
    /// シーンをまたがないTitleシーン限定のマッチング管理
    /// 参加者リスト・Ready管理・全員揃った後のシーン遷移トリガー等
    /// </summary>
    public class TitleMatchingManager : MonoBehaviour
    {
        public static TitleMatchingManager Instance { get; private set; }
        
        // 参加者一覧。値が変わる度、表示も更新させたいので購読対象にする
        private readonly ReactiveProperty<List<TitlePlayer>> _players = new(new List<TitlePlayer>());
        public ReadOnlyReactiveProperty<List<TitlePlayer>> Players => _players; // 外部参照用

        /// <summary>
        /// BootStrapクラスから呼ばれる
        /// </summary>
        public void Initialize()
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

            // 3人未満は成り立たないので判定しない
            if (players.Count < 3)
            {
                Debug.Log("人数が足りない！今…" + players.Count);
                return;
            }

            // 1人でも未Readyがいたら何もしない
            foreach (var player in players)
            {
                if (!player.IsReady) return;
            }

            // 開始確定！なので準備に取り掛かる
            ProceedToGameStart().Forget();
        }
        
        /// <summary>
        /// 全クライアントがそれぞれ独立して実行する開始処理。
        /// 自分のローカルなTitlePlayerリストがまだ全員分揃っていない可能性があるため、
        /// Fusionが権威的に管理しているSessionInfo.PlayerCountと一致するまで待ってから進める。
        /// </summary>
        private async UniTaskVoid ProceedToGameStart()
        {
            var runner = JankenNetworkManager.Instance.Runner;
            
            // ローカルで認識している人数が、実際の接続人数（全クライアント共通の値）と一致するまで待つ
            // 参加者リスト変動で毎回更新しているが待機処理でちゃんと確認しておきたい
            await UniTask.WaitUntil(() => _players.CurrentValue.Count >= runner.SessionInfo.PlayerCount);

            // 待機中に誰かがReadyを解除する可能性は低いが、念のため再確認する
            foreach (var player in _players.CurrentValue)
            {
                if (!player.IsReady) return;
            }
            
            // ゲームが開始中と判定きりかえ！（代表者だけで充分だけど）
            JankenNetworkManager.Instance.MarkSessionAsInGame();

            // シーン遷移の実行は、代表者だけ（全クライアント別々にLoadSceneはよくない）
            if (runner.IsSharedModeMasterClient)
                runner.LoadScene(SceneRef.FromPath("Assets/Scenes/InGame.unity"));
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
            // .Add(player)みたいな書き方だと通知いかないらしい。だから新しいリスト丸ごとを用意する必要があった
            _players.Value = list;
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