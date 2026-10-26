# PermissionController Force Respawn

Yodokorochan PermissionController 用の、独立したワールド空間パネルを追加する VPM パッケージです。管理者(Auth)と協力者(Collab)が対象プレイヤーを選ぶと、そのプレイヤーを本人のリスポン地点へ戻します(対象者のクライアントで `VRCPlayerApi.Respawn()` を実行)。

移動先は対象者自身の現在の権限に対応するリスポン地点です。自分でリスポンしたときと同じ場所へ移動し、移動先を選ぶ UI はありません。権限別の地点は PermissionController 本来の機能で管理します。sequential-spawn が導入されていれば、その機能も自然に適用されます。

## 必要条件

- Unity 2022.3
- VRChat Worlds SDK 3.8.1 以降(3.10.5 で検証)
- UdonSharp
- Yodokorochan PermissionController (別途導入)

sequential-spawn には依存しません。本パッケージには本パッケージ自身のコードだけを含み、PermissionController のソースやアセットを再配布・変更しません。上流との連携には、PlayerObject の `Controller` と `CurrentPermission` という名前のプログラム変数だけを使います。

## セットアップ

1. VCC からワールドプロジェクトへ本パッケージを追加します。
2. シーンでパネルを置きたい位置を選択してから、**Tools > TenteEEEE > PermissionController Force Respawn > Create Panel** (または **GameObject > TenteEEEE > PermissionController Force Respawn Panel**) を実行します。何も選択していないか PermissionController 自身(またはその子)を選択している場合は、上流の Canvas(パスワード入力のテンキーを含む)すべての右端から 0.1m 離れた位置に、`MainCanvas` と同じ回転・同じ大きさ(1.1m×0.9m)で生成します。`MainCanvas` が見つからない場合はコントローラー位置、コントローラーも無い場合は原点に生成します。
3. 生成されるワールド空間 Canvas に UI、行テンプレート、`VRCUiShape`、ボタンのイベント配線が含まれます。開いているシーンで最初に見つかった PermissionController が自動設定されます。見つからない場合は Inspector で設定してください。
4. パネルの `permissionController` がワールドで使う PermissionController を指していることを確認します。Inspector は未設定や上流フィールドとの非互換を表示します。PermissionController が未設定、またはパネルの UI 参照が欠けたままビルドするとエラーで止まります。

画面右上に自分の権限、一覧に入室順のプレイヤー・権限バッジ・リスポーンボタンを表示します。操作できない人には権限表示に「（閲覧のみ）」が付き、一覧が薄く表示されてボタンが押せなくなります。直近の結果は一覧の下端に数秒だけ表示されます。対象を選ぶと移動先の権限を示す確認が開き、この確認は操作者だけに表示されます。パネルは Editor のコードでシーン内に生成し、prefab は同梱しません。

強制リスポーン先を権限ごとに上書きしたい場合は、パネルの `authDestination` / `collabDestination` / `visitorDestination` に Transform を設定します。空欄の権限はその人の通常のリスポン地点へ移動します。上書きは強制リスポーンだけに効き、メニューからのリスポーンや落下時のリスポーンは上流 PermissionController の設定のままです。なお `visitorDestination` を設定すると、sequential-spawn を使っていても一般は割り当て地点ではなくその1地点へ移動します。

UI の文字には Unity 組み込みの `LegacyRuntime.ttf` と `UnityEngine.UI.Text` を使い、フォントアセットは再配布しません。

## 動作と権限

- 操作できるのは Auth と Collab です。パネルは全員に表示されますが、他の権限では操作ボタンが無効になります。
- 操作は既定ではパネルから2m以内でのみ有効です(Inspector の「操作範囲」で変更可)。離れた位置から押すと距離に応じた案内を表示し、受信側でも送信者の位置を検査します。
- 自分自身は一覧に表示しません。協力者は管理者も対象にできます。権限の上下関係による対象制限はありません。
- 確認ダイアログを見るのは操作者だけです。対象者には確認や通知を表示せず、拒否もできません。
- 確認後、引数付きネットワークイベントを送信します。各クライアントは、対象本人が受信したイベントであることと、送信者が現在 Auth または Collab で、パネルの操作範囲(既定2m)以内にいることを検査してから移動します(上書き地点があれば `TeleportTo`、無ければ `Respawn()`)。ボタンを隠すだけでなく、受信側でも権限を検証します。
- 受信側では、前回の強制リスポーンから1秒未満の要求を無視します。操作者側のボタンも送信後1.5秒は「送信済み」で押せません。
- 確認中に対象者が退室するとダイアログを閉じます。「リスポーンさせる」の押下時にも、対象者の在室と操作者の権限を再確認します。
- 対象者が椅子(Station)に座っている場合の挙動は、VRChat 標準の `Respawn()` に任せます。

## 負荷

- 同期変数はありません。ネットワーク通信は強制リスポーンを実行したときの1回だけです。
- 一覧の更新は、パネルから20m以内にいる間だけ1秒ごとに行います。離れている間は止まり、近づくと1秒以内に追いつきます。入退室は距離に関係なく反映します。
- 各プレイヤーの権限の読み取り元は初回に1度だけ探し、以降は変数1つを読むだけです。入室時に続けて届く入室イベントは1回の更新にまとめます。

権限値は現在の PermissionController の enum に従い、Auth = 0、Collab = 1、Visitor = 2 です。上流で `Controller` または `CurrentPermission` のフィールド名が変わると Inspector にエラーが表示され、パッケージの更新が必要です。

## ライセンス

MIT。詳細は [LICENSE](LICENSE) を参照してください。
