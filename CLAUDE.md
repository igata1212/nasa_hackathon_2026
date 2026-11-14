# 遠慮のNASA — NASA Space Apps 2026

お題：[Space Mission Design Game](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/)。宇宙ミッションの設計から運用までを、最大4人で協力して遊べるゲームにする。ゲームの中身は `docs/design/SPEC.md` を読む。

3人・2日間。迷ったら作らない。作るなら細部まで詰める。役割分担と2日間の予定は `docs/reference/planning-meeting.md`。

**docs との食い違いに気づいたら、作業を止めずにすぐ伝える。** 本人の指示・コード・ほかの資料が `docs/design/` と合わないときは、その場で一言言う（どのファイルの何と、何が違うか）。`docs/design/` が正。直すなら Issue にする。

## 最初に：誰が作業しているか

作業を始める前に `.me` を読む。1行目がその人の名前。

- ファイルがない、または空なら、名前を聞いて `.me` に書く（`.gitignore` 済み。コミットしない）
- この名前を、Issue・ログ・ドキュメントの「書いた人」に使う

| 名前 | GitHub | 担当 |
|---|---|---|
| iizuka | `tada246` | Unity・サーバー（Python）・通信・GMAT 連携・GitHub 運用 |
| Taiga | `igata1212` | 物語・台詞・パラメータ・UI（絵とレイアウトまで。Unity への組み込みは iizuka）・3D アセットなど上の層 |
| midori | `midori11111111` | 3D モデル（`modeling/PhysDraft-SpaceCrew3D`）、物語の英語レビューなど。海外在住・英語。この人向けの Issue は英語で書く |

Issue の担当を付けるときは GitHub の名前を使う：`gh issue edit N --add-assignee igata1212`

## 作業の流れ（Claude が全部やる）

ブランチを知らない人もいる。本人に git の操作をさせず、Claude が次の順に進める。

1. **始める**：`git switch main && git pull`。Issue を作る：`gh issue create --assignee @me --title "…" --body "…"`。番号 N を控える
2. **ブランチ**：`git switch -c issue/N-短い英語`（例：`issue/12-design-screen`）
3. **作業**：区切りごとにコミット。メッセージは `#N 何をしたか`（日本語でよい）
4. **終わる**：本人に「終わりでいい？」と確認してから
   - `docs/log/<名前>.md` に1行追記する（下のログの書式）
   - `git fetch origin && git merge origin/main` → `git push -u origin HEAD`
   - `gh pr create --fill --body "Closes #N"` → `gh pr merge --squash`（ブランチは消さない）
   - `git switch main && git pull`

守ること：

- main に直接コミット・プッシュしない。小さな修正でも Issue から
- 1つの Issue に1つのこと。大きくなったら分ける
- Issue を読むときはコメントも読む：`gh issue view N --comments`。決まったことや変更はコメントに書かれていることが多い
- 衝突したら、`.unity` `.prefab` `.asset` は自分で解決しない。誰の変更とぶつかったかを伝えて本人に聞く。それ以外は解決してよいが、何を選んだか伝える

## フォルダ

| 場所 | 中身 |
|---|---|
| `unity/` | Unity プロジェクトのルート（Unity Hub でここに作る） |
| `modeling/` | モデルを作るスクリプトとレンダー。FBX は `unity/Assets/Models/` へ。新しい `.blend` はコミットしない（スクリプトから作り直す）。ルールは `modeling/core/README.md` |
| `data/` | NASA のデータ、GMAT の出力など、ゲームが読む外部データ |
| `story/` | ゲームの物語、台詞の下書き |
| `docs/design/` | **最新の設計だけ**（SPEC、見た目、ゲームの流れ）。決まったことが変わったらここを直す |
| `docs/reference/` | 経緯の資料（議事録、調査、計画、参考画像）。古いことがあるので、`docs/design/` と違えば `docs/design/` が正 |
| `docs/log/` | 作業ログ（1人1ファイル） |
| `tmp/` | 作業用の置き場。コミットされない。使うものは正しい場所に移す |

## NASA のツール・データ

使う方向で考えているもの（確定ではない）：

- [GMAT](https://software.nasa.gov/software/GSC-19468-1)：軌道を計算する。帰還ルート A / B の軌道を出して `data/` に置き、Unity で描く（`docs/design/SPEC.md` 11・12章）
- [NASA Mission Viz](https://nasa.github.io/mission-viz/)：軌道やミッションを Web 上で 3D 表示する

ほかの候補は [Resources タブ](https://www.spaceappschallenge.org/2026/challenges/space-mission-design-game/?tab=resources)。

`data/` にファイルを足したら、`data/README.md` に「ファイル名・出典 URL・取った人」を1行ずつ書く。提出のときに、使った NASA のデータを一覧で出すため。

## ドキュメントの書き方

誰がやったかを必ず残す。

- **ログ** `docs/log/<名前>.md`：自分のファイルにだけ追記する（他人のファイルを触らないので衝突しない）

  ```markdown
  ## 2026-11-14
  - 14:30 #12 設計画面の部品リストを作った
  ```

- **設計・物語**：先頭に `書いた人：名前 ／ 最終更新：YYYY-MM-DD` を書く（`SPEC.md` と同じ形）。他人の文書を大きく変えたら `更新：名前（日付）` を足す

## Unity

- バージョンは全員 **6000.6.9f1**（URP）。Unity Hub で「Add → `unity/` フォルダ」で開く。違うバージョンで開くと全ファイルが書き換わるので、開く前に確認する
- **Unity MCP**（Claude から Unity を操作する。CoplayDev/unity-mcp v10.3.0）：Unity を開き、Window → MCP for Unity で Start Server を押してから Claude Code を開き直す。接続先はリポジトリ直下の `.mcp.json`。Unity を閉じると使えない。`uv` が要る
- `.meta` ファイルは必ずアセットと一緒にコミットする（片方だけだと参照が壊れる）
- シーンは1人1つを基本にする。共有シーンを触るときは Issue に書いておく
- **数値はコードに書かない**。`parameters.json` / `parts.json` から読む。JSON は `docs/design/` の xlsx から作り、`reference_sim.py` と同じフォルダに置く（同じフォルダから読むため）。ロジックで迷ったら `docs/design/reference_sim.py` が正
- シミュレーションはサーバー（Python）で動かす。Unity に移植しない（`docs/design/architecture.md`）
- 画面に出る文字列は英語（提出用）

## ファイル

- 50MB を超えるファイルはコミットしない。必要なら相談する
- `.blend1` などの自動バックアップ、Unity の `Library/` などは `.gitignore` 済み
