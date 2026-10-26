# PermissionController Force Respawn

[よどころちゃん様の PermissionController](https://yodokoro.booth.pm/items/7174370) 用のパネルです。**管理者・協力者が、選んだプレイヤーをその人のリスポン地点へ戻せます。**

[English](README.md) · [VPM リポジトリ](https://tenteeeee.github.io/vpm-repos/) · [パッケージの詳細](Packages/com.tentee.permission-controller-force-respawn/README.ja.md)

- 一覧からプレイヤーを選んで確認すると、その人**自身の**権限のリスポン地点(自分でリスポンしたときと同じ場所)へ移動します。強制リスポーン時だけの行き先を権限ごとに上書きすることもできます。
- 操作できるのは管理者と協力者だけで、パネルから **2m 以内**(既定値・Inspector で変更可)に限ります。送信者の権限と距離は対象者のクライアントでも検査するので、権限の無い人や離れた場所からネットワークイベントを送っても受け付けません。
- 確認ダイアログは操作者だけに表示され、対象者は拒否できません。
- パネルは PermissionController の既定の画面と同じ大きさ(1.1m×0.9m)・同じ配色で、その隣に自動配置されます。
- **PermissionController のファイルは一切変更・再配布しません。** 上流のプログラム変数 2 つ(`Controller`、`CurrentPermission`)を名前で読むだけです。

## 使い方

1. プロジェクトに [PermissionController](https://yodokoro.booth.pm/items/7174370) を導入します。
2. VRChat Creator Companion にカスタム VPM リポジトリ `https://tenteeeee.github.io/vpm-repos/index.json` を追加し、**PermissionController Force Respawn** を追加します。
3. Unity で **Tools > TenteEEEE > PermissionController Force Respawn > Create Panel** を実行します(または Hierarchy で PermissionController を右クリックして **TenteEEEE > PermissionController Force Respawn Panel**)。

## 開発

パッケージ本体は [`Packages/com.tentee.permission-controller-force-respawn/`](Packages/com.tentee.permission-controller-force-respawn/) です。コミット前に `python -X utf8 Tools/verify-package.py` を実行してください。

[`Tools/ClientSim/`](Tools/ClientSim/) は開発時に使った batchmode テストハーネスです(ClientSim 上の実 Udon と模擬リモートプレイヤー)。PermissionController と本パッケージを入れたプロジェクトの `Assets/<tmp>/Editor/` に `ForceRespawnSim.cs`、`Assets/<tmp>/` に `ForceRespawnSimDriver.cs` を置き、`-quit` なしで `-executeMethod ForceRespawnSim.RunBatch` を実行します。

リリースは `x.x.x` / `vx.x.x` のタグを push すると公開されます。

## ライセンス

[MIT](LICENSE)。PermissionController 自体のライセンスは作者に帰属します。
