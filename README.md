# CodeMap

CodeMap は、Windows 向けのソースコード可視化アプリです。  
WinUI 3 のデスクトップ UI と WebView2 のグラフ表示を組み合わせて、コードベースの構造を俯瞰できます。

## 主な機能

- `.sln` / `.slnx` / `.csproj` / `.vcxproj` / フォルダーをワークスペースとして読み込み
- C#（Roslyn）と C/C++ の解析
- プロジェクト / ファイル / シンボル / パッケージ / アセンブリ / DLL 依存のグラフ表示
- グラフの検索、依存マップ、影響範囲、循環依存のみ表示、ノード固定・非表示
- 左ペインのツリー表示とシンボル一覧表示
- 解析結果の SQLite キャッシュ保存と「前回結果」読み込み
- 日本語 / 英語 UI、ライト / ダークテーマ、主要ショートカット対応

## 必要環境

- Windows 10 (build 17763) 以降
- .NET 10 SDK
- TypeScript コンパイラ (`tsc`) が PATH 上で利用可能

`tsc` が未導入の場合、`CodeMap.csproj` の `CompileGraphTypeScript` ターゲットでビルドが失敗します。

## 開発用コマンド

```powershell
dotnet build .\CodeMap.slnx -c Release
dotnet test .\CodeMap.slnx -c Release
```

## 保存先

- 解析キャッシュ: `%LocalAppData%\CodeMap\analysis-cache.db`
- ログ: `%LocalAppData%\CodeMap\logs\codemap.log`
- 設定 / 最近使ったワークスペース: `%LocalAppData%\CodeMap\`

## リポジトリ構成

- `CodeMap/`: アプリ本体
- `CodeMap/Analysis/`: C# / C/C++ 解析
- `CodeMap/Graph/`: グラフ用ペイロード生成
- `CodeMap/Storage/`: SQLite 保存 / 読み込み
- `CodeMap/Web/Graph/`: WebView2 のグラフ UI
- `CodeMap/Web/third_party/`: サードパーティー資産
