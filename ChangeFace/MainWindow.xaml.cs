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
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Samples.Kinect.WpfViewers;

namespace ChangeFace
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        bool isWindowsClosing = false;//窗口是否正在关闭
        const int MaxSkeletonTackingCount = 6;//最多可以同时跟踪的用户数
        Skeleton[] allSkeletons = new Skeleton[MaxSkeletonTackingCount];
        int faceIndex = 0;//脸谱的编号
        
        private KinectSensorChooser _kinectSensorChooser = new KinectSensorChooser();
        private KinectSensorManager sensorManager;

        public MainWindow()
        {
           //初始化组件
            InitializeComponent();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //隐藏道具
            hideFace();
            this.kinectSensorChooserUi.KinectSensorChooser = _kinectSensorChooser;
            sensorManager = new KinectSensorManager();
            _kinectSensorChooser.Start();
            sensorManager.KinectSensorChanged += SensorManager_KinectSensorChanged;
            sensorManager.KinectSensor = _kinectSensorChooser.Kinect;
            if (sensorManager.KinectSensor != null)
                Console.WriteLine("*** Kinect Ready! ***");
            MessageBox.Show("当前连接的kinect个数：" + KinectSensor.KinectSensors.Count.ToString() + "\nkinctSensorChooser当前状态：" + _kinectSensorChooser.Status + "\nkinect当前状态：" + _kinectSensorChooser.Kinect.Status);

            _kinectSensorChooser.Start();
        }

        private void SensorManager_KinectSensorChanged(object sender, KinectSensorManagerEventArgs<KinectSensor> e)
        {
            KinectSensor oldKinect = e.OldValue;
            stopKinect(oldKinect);
            KinectSensor kinect = e.NewValue;
            if (kinect == null)
            {
                return;
            }
            kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            kinect.SkeletonStream.Enable();
            kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(Kinect_AllFramesReady);
            try
            {
                //显示彩色摄像头
                kinectColorViewer1.KinectSensorManager = sensorManager;
                kinectColorViewer1.KinectSensorManager.KinectSensor = kinect;
                //启动
                kinect.Start();

            }
            catch (System.IO.IOException)
            {
                Console.WriteLine("hah");
            }
        }

        //关闭窗口
        private void Window_Closing(object sender,System.ComponentModel.CancelEventArgs  e)
        {
            isWindowsClosing = true;
            stopKinect(_kinectSensorChooser.Kinect);
        }


        //处理骨骼追踪的核心代码
        private void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if(isWindowsClosing)
            {
                return;
            }
            //仅获取第一个被骨骼追踪的用户
            Skeleton first = GetFirstSkeleton(e);
            if (null == first)
            {
                hideFace();
                return;
            }
            //成功骨骼追踪后再显示道具
            if (first.TrackingState != SkeletonTrackingState.Tracked)
            {
                hideFace();
                return;
            } else
            {
                 // showFace();
                //坐标映射
                mappingSkeleton2CameraCoordinate(first, e);
                changeFace(first);
            }
        }

        //开始变脸
        private void mappingSkeleton2CameraCoordinate(Skeleton first, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if(depth == null || _kinectSensorChooser.Kinect == null)
                {
                    return;
                }
                //将骨骼坐标点直接映射到彩色图像坐标点
                ColorImagePoint headColorPoint = _kinectSensorChooser.Kinect.MapSkeletonPointToColor(first.Joints[JointType.Head].Position, ColorImageFormat.RgbResolution640x480Fps30);
                ColorImagePoint leftColorPoint = _kinectSensorChooser.Kinect.MapSkeletonPointToColor(first.Joints[JointType.HandLeft].Position, ColorImageFormat.RgbResolution640x480Fps30);
                ColorImagePoint rightColorPoint = _kinectSensorChooser.Kinect.MapSkeletonPointToColor(first.Joints[JointType.HandRight].Position, ColorImageFormat.RgbResolution640x480Fps30);
                //修正为中心位置
                adjustCameraPosition(headImage, headColorPoint);
                adjustCameraPosition(leftEclipe, leftColorPoint);
                adjustCameraPosition(rightEclipe, rightColorPoint);
            }
        }

        //改变脸谱图片坐标
        private void adjustCameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            //从对象的（left，top）修正为该对象的中心位置
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 4);
        }

        //显示脸谱道具
        private void changeFace(Skeleton first)
        {
            //判断左右手和头部骨骼是否在重合范围之类
            if(isTwoSkeletonPointOverLapping(first.Joints[JointType.Head].Position,first.Joints[JointType.HandRight].Position) 
                || isTwoSkeletonPointOverLapping(first.Joints[JointType.Head].Position,first.Joints[JointType.HandLeft].Position)) {
                faceIndex++;
                faceIndex %= 5;
                if(faceIndex == 0)
                {
                    //抹去川剧脸谱
                    headImage.Source = null;
                } else
                {
                    string faceImgSource = "pack://application:,,,/images/face" + faceIndex + ".png";
                    headImage.Source = new BitmapImage(new Uri(faceImgSource));
                }
            }
        }

        //判断两个skeletonPoint点是否在同一个区域
        private bool isTwoSkeletonPointOverLapping(SkeletonPoint position1, SkeletonPoint position2)
        {
            bool isOverLapping = (Math.Abs(position1.X - position2.X) <= 0.05) && (Math.Abs(position1.Y - position2.Y) <= 0.05);
            return isOverLapping;
        }

        //获取第一个被骨骼追踪的用户
        private Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return null;
                skeletonFrame.CopySkeletonDataTo(allSkeletons);
                //LINQ语法，用于查找第一个被追踪的骨骼
                Skeleton first = (from s in allSkeletons where s.TrackingState == SkeletonTrackingState.Tracked select s).FirstOrDefault();
                return first;
            }
        }

        private void stopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if(sensor.IsRunning)
                {
                    sensor.Stop();
                    //如果当前音频流已经打开，关闭
                    if(sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }
                }
            }
        }

        //不显示脸谱图片
        private void hideFace()
        {
            headImage.Source = null;
        }

        //显示脸谱道具
        private void showFace()
        {
            faceIndex++;
            string faceImgSource = "pack://application:,,,/images/face1.png";
            headImage.Source = new BitmapImage(new Uri(faceImgSource));

        }

        private void Button_Start(object sender, RoutedEventArgs e)
        {
            //kinect.Start();
            _kinectSensorChooser.Kinect.Start();
            sensorManager.KinectSensor.Start();
            //Console.WriteLine(_kinectSensorChooser.Status.ToString());
            MessageBox.Show("\nkinctSensorChooser当前状态：" + _kinectSensorChooser.Status.ToString() + "\nkinctr当前状态： " + _kinectSensorChooser.Kinect.Status.ToString());
        }

        private void Button_Down(object sender, RoutedEventArgs e)
        {
            _kinectSensorChooser.Kinect.Stop();
            sensorManager.KinectSensor = _kinectSensorChooser.Kinect;
            sensorManager.KinectSensor.Stop();
            MessageBox.Show("\nkinctSensorChooser当前状态：" + _kinectSensorChooser.Status.ToString() + "\nkinct当前状态： " + _kinectSensorChooser.Kinect.Status.ToString());
            //Console.WriteLine(_kinectSensorChooser.Status.ToString());
        }

    }

}
