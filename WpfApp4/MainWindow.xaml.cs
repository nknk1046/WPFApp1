using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace WpfApp4
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジックです
    /// </summary>
    /// 

    public partial class MainWindow : System.Windows.Window
    {

        //カメラキャプチャのクラスを定義
        public bool IsExitCapture { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// カメラ画像を取得して次々に表示を切り替える
        /// </summary>
        public virtual void Capture(object state)
        {
            var camera = new VideoCapture(0/*0番目のデバイスを指定*/)
            {
                // キャプチャする画像のサイズフレームレートの指定
                FrameWidth = 480,
                FrameHeight = 270,
                // Fps = 60
            };

            using (var img = new Mat()) // 撮影した画像を受ける変数
            using (camera)
            {
                while (true)
                {
                    if (this.IsExitCapture)
                    {
                        this.Dispatcher.Invoke(() => this._Image.Source = null);
                        break;
                    }

                    camera.Read(img); // Webカメラの読み取り（バッファに入までブロックされる

                    if (img.Empty())
                    {
                        break;
                    }

                    this.Dispatcher.Invoke(() =>
                    {
                        this._Image.Source = img.ToWriteableBitmap(); // WPFに画像を表示
                    });
                }
            }
        }


        //ボタン（画像読込）クリック時の動作
        private async void Browsebutton_Click(object sender, RoutedEventArgs e)
        {
            //画像を選択して表示する
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            if (!(bool)result)
            {
                return;
            }

            //ファイルパスの取得
            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;
            //Debug.WriteLine("Test1");

            //AzureのFaceAPIと接続
            //リクエストURLとkey
            string base_url = "https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect";
            string request_param = "returnFaceId=true&returnFaceLandmarks=false" +
                "&returnFaceAttributes=age,gender,headPose,smile,facialHair,glasses," +
                "emotion,hair,makeup,occlusion,accessories,blur,exposure,noise";
            string face_url = base_url + "?" + request_param;
            //string face_url = "https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false";
            string face_key = "ab4a56df77124c149575bd4a94560c44";

            //送信するJSONデータを定義
            //var postdata = "{\"url\":\"" + filePath + "\"}";
            //var content = new StringContent(postdata, Encoding.UTF8, "application/octet-stream");
            //Debug.WriteLine(postdata);

            //HTTPクライアント作成、ヘッダーにキーを設定
            HttpClient faceClient = new HttpClient();
            faceClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", face_key);

            //リクエストBodyの作成（関数呼び出し）
            byte[] byteData = GetImageAsByteArray(filePath);

            //Azureに接続
            HttpResponseMessage response;
            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json"
                // and "multipart/form-data".
                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await faceClient.PostAsync(face_url, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();
                //JObject jres = JObject.Parse(contentString);

                /* weather newsで試した例
                string baseUrl = "http://weather.livedoor.com/forecast/webservice/json/v1";
                //東京都のID
                string cityname = "130010";

                string url = $"{baseUrl}?city={cityname}";
                string json = new HttpClient().GetStringAsync(url).Result;
                JObject jobj = JObject.Parse(json);
                */

                // Display the JSON response.
                Console.WriteLine("\nResponse:\n");
                Console.WriteLine(contentString);
                ResultBox.Text = contentString;
                Console.WriteLine("\nPress Enter to exit...");

                //JSONを配列に入れる
                JArray face_array = JArray.Parse(contentString);

                //配列の中身を確認
                //Console.WriteLine(face_array[0]["faceAttributes"]["gender"].ToString());

                //顔の数だけ処理をする
                foreach (JObject item in face_array)
                {
                    JValue gender_value = (JValue)item["faceAttributes"]["gender"];
                    string gender = (string)gender_value.Value;
                    Console.WriteLine(gender);

                    JValue happiness_value = (JValue)item["faceAttributes"]["emotion"]["happiness"];
                    string happiness = happiness_value.Value.ToString();
                    Console.WriteLine(happiness);
                    string condition = "";

                    if(float.Parse(happiness) > 0.5)
                    {
                        condition = "いい体調ですね！";
                        Console.WriteLine(condition);
                        ConditionResult.Text = condition;

                    }
                            else
                    {
                        condition = "体調がよくなさそうです！";
                        Console.WriteLine(condition);
                        ConditionResult.Text = condition;
                    }

                }



            }

            //リクエストの作成
            //HttpClient faceClient = new HttpClient();
            //Uri face_uri = new Uri(face_url);
            //Debug.WriteLine("uri:"+ face_uri);
            //Debug.WriteLine("url:"+ face_url);
            //faceClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            //faceClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", face_key);

            //Azureへ接続してレスポンス取得
            //var respose = await faceClient.PostAsync("https://southeastasia.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false", content);
            //Debug.WriteLine("Test2" + respose);

            /*
            //リクエストの作成
            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(face_url);
            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
            wr.Headers.Add("Ocp-Apim-Subscription-Key", face_key);

            //接続
            Stream rs = wr.GetRequestStream();
            string responseStr = null;

            //送信するデータ（画像データとバイト数）き込む
            string postdata = "{\"url\": filePath}";
            byte[] postdatabytes = System.Text.Encoding.UTF8.GetBytes(postdata);
            rs.Write(postdatabytes, 0, postdatabytes.Length);
            rs.Close();
            */

        }

        private byte[] GetImageAsByteArray(string filePath)
        {
            using (FileStream fileStream =
                new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
           //throw new NotImplementedException();
        }
    }
}
