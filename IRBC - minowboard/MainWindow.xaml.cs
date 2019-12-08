//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.IO.Ports;
    using System.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.Windows.Threading;
    using System.Runtime.InteropServices;
    using System.Windows.Documents;
    using System.Windows.Navigation;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Kinect.Toolkit.Controls;
    using Microsoft.Kinect.Toolkit.Interaction;
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool switchMode = false;
        private KinectSensorChooser sensorChooser;
        long beginTime,endTime;
        int temp = 0;

        /*********Mobile Platform Variables***************/
       bool Send_status = true;
     //  TcpClient client = new TcpClient("192.168.1.1", 2001);
     //  TcpClient client;
       NetworkStream stream;//data transform 
     /*********************************************/
        TransformSmoothParameters smoothParameters;
        //defined below joints of right hand、right wrist、rightelbow、right shoulder、left shoulder 、left elbow 、left wrist 、left hand、shoulder center 、spine、head
        Joint jpr, jprw, jpre, jprs,jpsc, jpspine,jpls,jple,jplw,head;
        float rightWristAngle, rightElbowAngle, rightshouderAngle, waistturnAngle, leftshouderAngle, rightheaddir, leftElbowAngle;
        float rightWristAngle2 = 90, rightElbowAngle2 = 90, rightshouderAngle2 = 90, waistturnAngle2 = 45, leftshouderAngle2 = 90, rightheaddir2 = 37, leftElbowAngle2=0;
    
        /// <summary>
        /// Width of output drawing    
        /// </summary>
        private const float RenderWidth = 640.0f; 

        /// <summary>
        /// Height of our output drawing     
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines  
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse 
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles  
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point  
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked  
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred  
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked  
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred  
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor  
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output  
        /// </summary>
        private DrawingGroup drawingGroup;  

        /// <summary>
        /// Drawing image that we will display 
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class. 
        /// </summary>
        public MainWindow()
        {
            Loaded += OnLoaded;
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooser.KinectChanged += SensorChooserOnKinectChanged;
            this.sensorChooserUi.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.Start();
           
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs args)
        {
            bool error = false;
            if (args.OldSensor != null)
            {
                try
                {
                    args.OldSensor.DepthStream.Range = DepthRange.Default;
                    args.OldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    args.OldSensor.DepthStream.Disable();
                    args.OldSensor.SkeletonStream.Disable();
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                    error = true;
                }
            }

            if (args.NewSensor != null)
            {
                try
                {
                    args.NewSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    args.NewSensor.SkeletonStream.Enable();

                    try
                    {
                        args.NewSensor.DepthStream.Range = DepthRange.Near;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = true;
                        args.NewSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    }
                    catch (InvalidOperationException)
                    {
                        // Non Kinect for Windows devices do not support Near mode, so reset back to default mode.
                        args.NewSensor.DepthStream.Range = DepthRange.Default;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }
                }
                catch (InvalidOperationException)
                {
                    error = true;
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
                try
                {
                    if (!error)
                        kinectRegion.KinectSensor = args.NewSensor;
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }
        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data 绘制指示器去显示哪个边缘是修剪的骨骼数据
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks  
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
              //  client = new TcpClient("192.168.1.1", 2001);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
           
            beginTime = DateTime.Now.Ticks; ;

          //  stream = client.GetStream();//

            //Sets the amount of smoothing when processing bone data frames. 
            //It accepts a floating point value of 0-1. 
            //The larger the value, the more smooth it is. 0 means not smooth 
            smoothParameters.Smoothing = 0.5f; 

            // Create the drawing group we'll use for drawing 
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control 
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control 
            Image.Source = this.imageSource;

            // Browse all sensors and open the first connected sensor
            //This needs to be connected at the current moment when the program starts
            //
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames 
                this.sensor.SkeletonStream.Enable(smoothParameters);

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!  
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }
       
        /// <summary>
        /// Execute shutdown tasks  
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        //    stream.Close();
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event  
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size 
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);

                           if(skel.Joints[JointType.HandRight].TrackingState == JointTrackingState.Tracked)
                                jpr = skel.Joints[JointType.HandRight]; //右手
                           if (skel.Joints[JointType.WristRight].TrackingState == JointTrackingState.Tracked)
                                jprw = skel.Joints[JointType.WristRight];//右腕关节
                           if (skel.Joints[JointType.ElbowRight].TrackingState == JointTrackingState.Tracked)
                                jpre = skel.Joints[JointType.ElbowRight];//右肘关节
                           if (skel.Joints[JointType.ShoulderRight].TrackingState == JointTrackingState.Tracked)
                                jprs = skel.Joints[JointType.ShoulderRight];//右肩关节
                           if (skel.Joints[JointType.ShoulderCenter].TrackingState == JointTrackingState.Tracked)
                                jpsc = skel.Joints[JointType.ShoulderCenter];//两肩中心
                           if (skel.Joints[JointType.ShoulderLeft].TrackingState == JointTrackingState.Tracked)
                                jpls = skel.Joints[JointType.ShoulderLeft];//左肩关节
                           if (skel.Joints[JointType.ElbowLeft].TrackingState == JointTrackingState.Tracked)
                                jple = skel.Joints[JointType.ElbowLeft];//左肩关节
                           if (skel.Joints[JointType.Spine].TrackingState == JointTrackingState.Tracked)
                                jpspine = skel.Joints[JointType.Spine];//脊柱关节
                           if (skel.Joints[JointType.Head].TrackingState == JointTrackingState.Tracked)
                                head = skel.Joints[JointType.Head];//头
                           if (skel.Joints[JointType.WristLeft].TrackingState == JointTrackingState.Tracked)
                               jplw = skel.Joints[JointType.WristLeft];//头

                                rightWristAngle = CalculateWristAngleForWrist(jpr, jprw, jpre);
                                rightElbowAngle = CalculateAngleForElbow(jprw, jpre, jprs);
                                leftElbowAngle = CalculateAngleForElbow(jplw, jple, jpls);
                                rightshouderAngle = CalculateAngleForShouder(jpre, jprs, jpsc, jpspine);
                                waistturnAngle = CalculateAngleForShouder(jpre, jprs, jpls);
                                leftshouderAngle = CalculateAngleForShouder(jple, jpls, jpsc, jpspine);
                                rightheaddir = CalculateAngleForShouder(head, jpsc, jprs);
                                label7.Content = rightheaddir;
                                if (switchMode) //swith mode || rightheaddir < 50
                                {
                                //rightWristAngle = AmplitudeLimiterFilter(rightWristAngle, rightWristAngle2);
                                //rightElbowAngle = AmplitudeLimiterFilter(rightElbowAngle, rightElbowAngle2);
                                //rightshouderAngle = AmplitudeLimiterFilter(rightshouderAngle, rightshouderAngle2);
                                //waistturnAngle = AmplitudeLimiterFilter(waistturnAngle, waistturnAngle2);
                                //leftshouderAngle = AmplitudeLimiterFilter(leftshouderAngle, leftshouderAngle2);
                                //long endTime2 = DateTime.Now.Ticks;
                                //while (DateTime.Now.Ticks - endTime2 < 2000) ;

                                endTime = DateTime.Now.Ticks;
                                if (endTime - beginTime > 2000)  //delay 2s
                                {
                                    if (temp == 0)  //value last time
                                    {

                                        rightWristAngle2 = rightWristAngle;
                                        leftElbowAngle2 = leftElbowAngle;
                                        rightElbowAngle2 = rightElbowAngle;
                                        rightshouderAngle2 = rightshouderAngle;
                                        waistturnAngle2 = waistturnAngle;
                                        leftshouderAngle2 = leftshouderAngle;
                                        rightheaddir2 = rightheaddir;
                                        temp++;
                                    }
                                    else
                                    {
                                        rightWristAngle = (float)(rightWristAngle * 0.7 + rightWristAngle2 * 0.3);
                                        rightElbowAngle = (float)(rightElbowAngle * 0.7 + rightElbowAngle2 * 0.3);
                                        leftElbowAngle = (float)(leftElbowAngle * 0.7 + leftElbowAngle2 * 0.3);
                                        rightshouderAngle = (float)(rightshouderAngle * 0.7 + rightshouderAngle2 * 0.3);
                                        waistturnAngle = (float)(waistturnAngle * 0.7 + waistturnAngle2 * 0.3);
                                        leftshouderAngle = (float)(leftshouderAngle * 0.7 + leftshouderAngle2 * 0.3);
                                        rightheaddir = (float)(rightheaddir * 0.7 + rightheaddir2 * 0.3);

                                        //rightWristAngle = (rightWristAngle + rightWristAngle2) / 2;
                                        //rightElbowAngle = (rightElbowAngle + rightElbowAngle2) / 2;
                                        //rightshouderAngle = (rightshouderAngle + rightshouderAngle2) / 2;
                                        //waistturnAngle = (waistturnAngle + waistturnAngle2) / 2;
                                        //leftshouderAngle = (leftshouderAngle + leftshouderAngle2) / 2;

                                        textBox1.Text = Convert.ToString(leftshouderAngle);//rightWristAngle replaced by left arm

                                        textBox2.Text = Convert.ToString(rightElbowAngle);

                                        textBox3.Text = Convert.ToString(leftElbowAngle);

                                        textBox5.Text = Convert.ToString(rightshouderAngle);

                                        //textBox3.Text = Convert.ToString(leftshouderAngle);
                                        try
                                        {
                                            scrollBar1.Value = leftshouderAngle;//rightWristAngle replaced by left arm
                                            byte[] data1 = { 0xFF, 0X01, 0X01, Convert.ToByte(scrollBar1.Value), 0XFF };
                                            //stream.Write(data1, 0, data1.Length);

                                            if (leftElbowAngle > 60 && leftElbowAngle < 120)
                                            {
                                                scrollBar3.Value = 150;
                                                byte[] data3 = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
                                                //stream.Write(data3, 0, data3.Length);
                                            }
                                            else
                                            {
                                                scrollBar3.Value = 30;
                                                byte[] data3 = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
                                                //stream.Write(data3, 0, data3.Length);
                                            }
                                            //scrollBar3.Value = leftElbowAngle;
                                            //byte[] data3 = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
                                            //stream.Write(data3, 0, data3.Length);

                                            scrollBar2.Value = rightElbowAngle;
                                            byte[] data2 = { 0xFF, 0X01, 0X02, Convert.ToByte(scrollBar2.Value), 0XFF };
                                            //stream.Write(data2, 0, data2.Length);

                                            scrollBar5.Value = rightshouderAngle;
                                            byte[] data5 = { 0xFF, 0X01, 0X05, Convert.ToByte(scrollBar5.Value), 0XFF };
                                            //stream.Write(data5, 0, data5.Length);

                                            scrollBar6.Value = waistturnAngle;
                                            byte[] data6 = { 0xFF, 0X01, 0X06, Convert.ToByte(scrollBar6.Value), 0XFF };
                                            //stream.Write(data6, 0, data6.Length);
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show(ex.Message);
                                        }
                                        rightWristAngle2 = rightWristAngle;
                                        rightElbowAngle2 = rightElbowAngle;
                                        leftElbowAngle2 = leftElbowAngle;
                                        rightshouderAngle2 = rightshouderAngle;
                                        waistturnAngle2 = waistturnAngle;
                                        leftshouderAngle2 = leftshouderAngle;
                                        rightheaddir2 = rightheaddir;
                                    }
                                    beginTime = endTime;

                                }
                            }
                            
                           
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }
                // prevent drawing outside of our render area  
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }
        private float AmplitudeLimiterFilter(float newValue,float Value)
        { 
            float  ReturnValue;
            if (((newValue - Value) > 10) || ((Value - newValue) > 10))
            {

                ReturnValue = Value;
            }
            else
            {
               // Value = newValue;
                ReturnValue = newValue;
            }
            return (ReturnValue);
        }
       
        /// <summary>
        /// do calculation 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        private float CalculateWristAngleForWrist(Joint a, Joint b, Joint c)  //计算右手腕角度
        {
            float vectorX1, vectorX2, vectorY1, vectorY2, vectorZ1, vectorZ2;
            float distab, distbc, cosAngle, Angle;
            vectorX1 = a.Position.X - b.Position.X;

            vectorY1 = a.Position.Y - b.Position.Y;

            vectorZ1 = a.Position.Z - b.Position.Z;

            vectorX2 = c.Position.X - b.Position.X;

            vectorY2 = c.Position.Y - b.Position.Y;

            vectorZ2 = c.Position.Z - b.Position.Z;

            distab = (float)Math.Sqrt(vectorX1 * vectorX1 + vectorY1 * vectorY1 + vectorZ1 * vectorZ1);

            distbc = (float)Math.Sqrt(vectorX2 * vectorX2 + vectorY2 * vectorY2 + vectorZ2 * vectorZ2);

            cosAngle = (vectorX1 * vectorX2 + vectorY1 * vectorY2 + vectorZ1 * vectorZ2) / (distab * distbc);

            //Angle = (float)(90-Math.Acos(cosAngle)*180/Math.PI);
           /* if (vectorY1 >= 0)
                Angle = (float)Math.Acos(cosAngle);
            else
                Angle = (float)(2 * Math.PI - Math.Acos(cosAngle));
            Angle = (float)(Angle * 180 * 3.0 / Math.PI -450);  //150-210 对应 0-180*/
            if (vectorY1 >= 0)
                Angle = (float)Math.Acos(cosAngle);
            else
                Angle = (float)(2 * Math.PI - Math.Acos(cosAngle));
            Angle = (float)(270 - Angle * 180 * 1.0 / Math.PI); 
            return Angle;
        }
        /// <summary>
        /// 计算右手肘关节的角度
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        private float CalculateAngleForElbow(Joint a, Joint b, Joint c)   //计算右手肘角度
        {
            float vectorX1, vectorX2, vectorY1, vectorY2, vectorZ1, vectorZ2;
            float distab, distbc, cosAngle, Angle;
            vectorX1 = a.Position.X - b.Position.X;

            vectorY1 = a.Position.Y - b.Position.Y;

            vectorZ1 = a.Position.Z - b.Position.Z;

            vectorX2 = c.Position.X - b.Position.X;

            vectorY2 = c.Position.Y - b.Position.Y;

            vectorZ2 = c.Position.Z - b.Position.Z;

            distab = (float)Math.Sqrt(vectorX1 * vectorX1 + vectorY1 * vectorY1 + vectorZ1 * vectorZ1);

            distbc = (float)Math.Sqrt(vectorX2 * vectorX2 + vectorY2 * vectorY2 + vectorZ2 * vectorZ2);

            cosAngle = (vectorX1 * vectorX2 + vectorY1 * vectorY2 + vectorZ1 * vectorZ2) / (distab * distbc);

            if (vectorY1 >= 0)
                Angle = (float)Math.Acos(cosAngle);
            else
                Angle = (float)(2 * Math.PI - Math.Acos(cosAngle));
            Angle = (float)(270 - Angle * 180 * 1.0 / Math.PI  ); 
            return Angle;
        }
        /// <summary>
        /// 计算肩关节的角度
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        private float CalculateAngleForShouder(Joint a, Joint b, Joint c, Joint d)  //计算肩关节的角度
        {
            float vectorX1, vectorX2, vectorY1, vectorY2, vectorZ1, vectorZ2;
            float distab, distbc, cosAngle, Angle;
            vectorX1 = a.Position.X - b.Position.X;

            vectorY1 = a.Position.Y - b.Position.Y;

            vectorZ1 = a.Position.Z - b.Position.Z;

            vectorX2 = d.Position.X - c.Position.X;

            vectorY2 = d.Position.Y - c.Position.Y;

            vectorZ2 = d.Position.Z - c.Position.Z;

            distab = (float)Math.Sqrt(vectorX1 * vectorX1 + vectorY1 * vectorY1 + vectorZ1 * vectorZ1);

            distbc = (float)Math.Sqrt(vectorX2 * vectorX2 + vectorY2 * vectorY2 + vectorZ2 * vectorZ2);

            cosAngle = (vectorX1 * vectorX2 + vectorY1 * vectorY2 + vectorZ1 * vectorZ2) / (distab * distbc);

                Angle = (float)Math.Acos(cosAngle);
          
            Angle = (float)(180 - Angle * 180 * 1.0 / Math.PI );  

            return Angle;

        }
        /// <summary>
        /// 计算腰的角度
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        private float CalculateAngleForShouder(Joint a, Joint b, Joint c)  //计算肩关节水平转动的角度
        {
            float vectorX1, vectorX2, vectorY1, vectorY2, vectorZ1, vectorZ2;
            float distab, distbc, cosAngle, Angle;
            vectorX1 = a.Position.X - b.Position.X;

            vectorY1 = a.Position.Y - b.Position.Y;

            vectorZ1 = a.Position.Z - b.Position.Z;

            vectorX2 = c.Position.X - b.Position.X;

            vectorY2 = c.Position.Y - b.Position.Y;

            vectorZ2 = c.Position.Z - b.Position.Z;

            distab = (float)Math.Sqrt(vectorX1 * vectorX1 + vectorY1 * vectorY1 + vectorZ1 * vectorZ1);

            distbc = (float)Math.Sqrt(vectorX2 * vectorX2 + vectorY2 * vectorY2 + vectorZ2 * vectorZ2);

            cosAngle = (vectorX1 * vectorX2 + vectorY1 * vectorY2 + vectorZ1 * vectorZ2) / (distab * distbc);

            Angle = (float)Math.Acos(cosAngle);

            Angle = (float)(180 - Angle * 180 * 1.0 / Math.PI);  

            return Angle;

        }
     
        private void getAllAngles()   //get all joints angle data
        {
            rightWristAngle = CalculateWristAngleForWrist(jpr, jprw, jpre);
            rightElbowAngle = CalculateAngleForElbow(jprw, jpre, jprs);
            rightshouderAngle = CalculateAngleForShouder(jpre, jprs, jpsc, jpspine);
            waistturnAngle = CalculateAngleForShouder(jpre, jprs,jpls);
            leftshouderAngle = CalculateAngleForShouder(jple, jpls, jpsc, jpspine);
        }
      
        /// <summary>
        /// Draws a skeleton's bones and joints 
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso 躯干
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm  左臂
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm 右臂
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg  左腿
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg 右腿
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 
            // Render Joints 关节，结合点
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point 
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            //----transform points to depth-----------
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints  
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit 
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred 
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked 
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box 
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                 //   this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                 //   this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X03, 0X00, 0XFF };
               // stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 5000) ;
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void Backward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X04, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 5000) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void Left_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X02, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 5000) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void Right_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X01, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 5000) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void LeftForward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X06, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 2500) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void RightForward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X07, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 2500) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void LeftBackward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X08, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 2500) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void RightBackward_Click(object sender, RoutedEventArgs e)
        {
            if (Send_status && switchMode == false)
            {
                byte[] data = { 0xFF, 0X00, 0X05, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
                Send_status = false;
            }
            //long begintime = DateTime.Now.Ticks;
            //while (DateTime.Now.Ticks - begintime < 2500) { }
            //Send_status = true;
            //byte[] datas = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
            //stream.Write(datas, 0, datas.Length);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            
                Send_status = true;
                byte[] data = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
                stream.Write(data, 0, data.Length);
        }
              
       private void scrollBar1_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox1.Text = Convert.ToString(scrollBar1.Value);
           byte[] data = { 0xFF, 0X01, 0X01, Convert.ToByte(scrollBar1.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }

       private void scrollBar2_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox2.Text = Convert.ToString(scrollBar2.Value);
           byte[] data = { 0xFF, 0X01, 0X02, Convert.ToByte(scrollBar2.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }

       private void scrollBar3_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox3.Text = Convert.ToString(scrollBar3.Value);
           byte[] data = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }

       private void scrollBar4_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox4.Text = Convert.ToString(scrollBar4.Value);
           byte[] data = { 0xFF, 0X01, 0X04, Convert.ToByte(scrollBar4.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }

       private void scrollBar5_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox5.Text = Convert.ToString(scrollBar5.Value);
           byte[] data = { 0xFF, 0X01, 0X05, Convert.ToByte(scrollBar5.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }
       private void scrollBar6_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
       {
           textBox6.Text = Convert.ToString(scrollBar6.Value);
           byte[] data = { 0xFF, 0X01, 0X06, Convert.ToByte(scrollBar6.Value), 0XFF };
           stream.Write(data, 0, data.Length);
       }
       int modeswitch = 0;
       private void KinectCircleButton_Click(object sender, RoutedEventArgs e)
       {
           if (modeswitch == 0)
           {
               switchMode = true;
               switchbutton.Content = "SWITCH OFF";
               modeswitch++;
           }
           else
           {
               switchMode = false;
               switchbutton.Content = "SWITCH ON";
               modeswitch--;
           }
       }
     
       int garbswitch = 0;
       private void KinectCircleButton_Click_2(object sender, RoutedEventArgs e)
       {
           if (garbswitch == 0)
           {
               scrollBar3.Value = 150;
               textBox3.Text = Convert.ToString(scrollBar3.Value);
               byte[] data3 = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
               stream.Write(data3, 0, data3.Length);//                                     data stream transfer
               grab.Content = "Grab";
               Color color = Color.FromRgb(255,0,0);
               grab.Foreground = new SolidColorBrush(color); 
               garbswitch++;
           }
           else
           {
               scrollBar3.Value = 30;
               textBox3.Text = Convert.ToString(scrollBar3.Value);
               byte[] data3 = { 0xFF, 0X01, 0X03, Convert.ToByte(scrollBar3.Value), 0XFF };
               stream.Write(data3, 0, data3.Length);
               grab.Content = "unGb";
               Color color = Color.FromRgb(0, 0, 0);
               grab.Foreground = new SolidColorBrush(color); 
               garbswitch--; 
           }
       }

       private void Forward_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X03, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void LeftForward_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X06, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void RightForward_MouseDown(object sender, MouseButtonEventArgs e)
       {

           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X07, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void Left_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X02, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void Right_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X01, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void LeftBackward_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X08, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void Backward_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X04, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void RightBackward_MouseDown(object sender, MouseButtonEventArgs e)
       {
           //if (Send_status && switchMode == false)
           //{
           //    byte[] data = { 0xFF, 0X00, 0X05, 0X00, 0XFF };
           //    stream.Write(data, 0, data.Length);
           //    Send_status = false;
           //}
       }

       private void KinectTitleButton_MouseUp(object sender, MouseButtonEventArgs e)
       {
           //Send_status = true;
           //byte[] data = { 0xFF, 0X00, 0X00, 0X00, 0XFF };
           //stream.Write(data, 0, data.Length);
       }
    }
}
