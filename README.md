# ARR を使って指定された時間でレスポンスを返す Azure のプラグイン
リバースプロキシとしてフロントにARRを設定し、バックエンドのIISで応答に時間がかかっている場合は、502を返します。

## 環境
以下の環境で作成しています。
* Visual Studio Ultimate 2012 SP1
* Windows Azure SDK for .NET 1.6

## 利用手順
1. Pluginフォルダの名前をARRReverseProxyに変更し、pluginsフォルダ（例：Program Files\Windows Azure SDK\v1.6\bin\plugins）にコピーします。
2. ServiceDefinition.csdef を編集し、ARRReverseProxyを追加します。
3. ロールの設定に ARRReverseProxy.Timeout が追加されますので、タイムアウト（秒）を設定します。

## 注意点
* 本プラグインを使用したサイトのエンドポイントのポート番号は、80以外にする必要があります。
* pluginsフォルダの中にある license.rtf は、Microsoft Web Platform Installer 3.0 のライセンス条項です。
