using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Timers;
using System.Runtime.InteropServices;
using Leap;

namespace LeapHelloWorld
{
    public partial class MainWindow : Window, ILeapEventDelegate
    {
        private Controller controller = new Controller();
        private LeapEventListener listener;
        private Boolean isClosing = false;

        struct MyGesture
        {
            public Boolean gestureOn;
            public Boolean gestureFinished;
            public int rightMotion;
            public int leftMotion;
            public int upMotion;
            public int downMotion;
            public int forwardMotion;
            public int backwardMotion;
        }

        MyGesture swipe = new MyGesture
        {
            gestureOn = false,
            gestureFinished = false,
            rightMotion = 0,
            leftMotion = 0,
            upMotion = 0,
            downMotion = 0
        };

        MyGesture wave = new MyGesture
        {
            gestureOn = false,
            gestureFinished = false,
            rightMotion = 0,
            leftMotion = 0
        };

        MyGesture poke = new MyGesture
        {
            gestureOn = false,
            gestureFinished = false,
            forwardMotion = 0,
            backwardMotion = 0
        };

        public MainWindow()
        {
            InitializeComponent();
            this.controller = new Controller();
            this.listener = new LeapEventListener(this);
            controller.AddListener(listener);
        }

        delegate void LeapEventDelegate(string EventName);
        public void LeapEventNotification(string EventName)
        {
            if (this.CheckAccess())
            {
                switch (EventName)
                {
                    case "onInit":
                        Debug.WriteLine("Init");
                        break;
                    case "onConnect":
                        Debug.WriteLine("Connected");
                        this.ConnectHandler();
                        break;
                    case "onFrame":
                        if (!this.isClosing)
                            this.CheckGestures(this.controller);
                        break;
                }
            }
            else
            {
                Dispatcher.Invoke(new LeapEventDelegate(LeapEventNotification), new object[] { EventName });
            }
        }

        void ConnectHandler()
        {
            this.controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
        }

        // Track gestures/movement frame-by-frame
        void CheckGestures(Leap.Controller controller)
        {
            FingerPosition(controller);

            if (!wave.gestureFinished)
            {
                WaveGesture(controller);
            }

            if (!swipe.gestureFinished)
            {
                SwipeGesture(controller);
            }

            if (!poke.gestureFinished)
            {
                PokeGesture(controller);
            }
        }

        // Close application
        void MainWindow_Closing(object sender, EventArgs e)
        {
            this.isClosing = true;
            this.controller.RemoveListener(this.listener);
            this.controller.Dispose();
        }

        // Map index finger position to screen
        void FingerPosition(Leap.Controller controller)
        {
            Leap.Frame frame = controller.Frame();
            Hand hand = frame.Hands[0];

            foreach (Finger finger in hand.Fingers)
            {
                if (finger.Type == Finger.FingerType.TYPE_INDEX && ((Pointable) finger).IsExtended)
                {
                    var position = ((Pointable)finger).TipPosition;
                    var normalized = frame.InteractionBox.NormalizePoint(position);

                    // Calculate mapped coordinates for finger
                    var x = ApplicationWindow.Width * normalized.x;
                    var y = ApplicationWindow.Height * normalized.z;

                    // Move on-screen FingerTracker box to mapped position
                    Canvas.SetLeft(FingerTracker, x);
                    Canvas.SetTop(FingerTracker, y);

                }
            }
        }

        // Wave Gesture
        void WaveGesture(Leap.Controller controller)
        {
            Leap.Frame frame = controller.Frame();
            Hand hand = frame.Hands[0];

            // Initiate Wave gesture if all 5 fingers are extended
            if (hand.Pointables.Count == 5 && !wave.gestureOn)
            {
                if (GetNumFingersExtended(hand) == 5)
                {
                    Debug.WriteLine("5 Fingers!");
                    wave.gestureOn = true;
                }
            }

            // Perform wave gesture if hand moves back and forth laterally
            if (wave.gestureOn)
            {
                Leap.Vector motion = frame.Translation(controller.Frame(1));

                if (motion.x > 7.0f)
                {
                    wave.rightMotion++;
                }
                else if (motion.x < -7.0f)
                {
                    wave.leftMotion++;
                }

                if (wave.rightMotion > 5 && wave.leftMotion > 5)
                {
                    Debug.WriteLine("Wave!");
                    wave.rightMotion = 0;
                    wave.leftMotion = 0;
                    wave.gestureOn = false;
                    wave.gestureFinished = true;
                }
            }
        }

        // Swipe Gesture
        void SwipeGesture(Leap.Controller controller)
        {
            Leap.Frame frame = controller.Frame();
            Hand hand = frame.Hands[0];

            // Initiate Swipe gesture if Index finger is extended
            if (!swipe.gestureOn)
            {
                foreach (Finger finger in hand.Fingers)
                {
                    if (finger.Type == Finger.FingerType.TYPE_INDEX && ((Pointable)finger).IsExtended)
                    {
                        Debug.WriteLine("Swipe Started");
                        swipe.gestureOn = true;
                    }
                }
            }

            // Perform swipe gesture if hand moves laterally
            if (swipe.gestureOn)
            {
                switch (GetNumFingersExtended(hand))
                {
                    case 1:
                        SwipeGesturePerform(controller, frame, 1);
                        break;
                    case 2:
                        SwipeGesturePerform(controller, frame, 2);
                        break;
                    case 5:
                        SwipeGesturePerform(controller, frame, 5);
                        break;
                }

            }

        }

        // Perform swipe gesture
        void SwipeGesturePerform(Leap.Controller controller, Leap.Frame frame, int numFingers)
        {
            Leap.Vector motion = frame.Translation(controller.Frame(1));

            // Increment/reset directional motion counters according to direction of swipe
            swipe.rightMotion = (motion.x > 12.0f) ? swipe.rightMotion + 1 : 0;
            swipe.leftMotion = (motion.x < -12.0f) ? swipe.leftMotion + 1 : 0;
            swipe.upMotion = (motion.z < -12.0f) ? swipe.upMotion + 1 : 0;
            swipe.downMotion = (motion.z > 12.0f) ? swipe.downMotion + 1 : 0;

            if (swipe.rightMotion > 5 || swipe.leftMotion > 5 || swipe.upMotion > 5 || swipe.downMotion > 5)
            {
                if (swipe.rightMotion > 5)
                {
                    switch (numFingers)
                    {
                        case 1:
                            Debug.WriteLine("Swipe Right 1!");
                            break;
                        case 2:
                            Debug.WriteLine("Swipe Right 2!");
                            break;
                        case 5:
                            Debug.WriteLine("Swipe Right 5!");
                            break;
                    }
                    swipe.rightMotion = 0;
                }
                else if (swipe.leftMotion > 5)
                {
                    switch (numFingers)
                    {
                        case 1:
                            Debug.WriteLine("Swipe Left 1!");
                            break;
                        case 2:
                            Debug.WriteLine("Swipe Left 2!");
                            break;
                        case 5:
                            Debug.WriteLine("Swipe Left 5!");
                            break;
                    }
                    swipe.leftMotion = 0;
                }
                else if (swipe.upMotion > 5)
                {
                    switch (numFingers)
                    {
                        case 1:
                            Debug.WriteLine("Swipe Up 1!");
                            break;
                        case 2:
                            Debug.WriteLine("Swipe Up 2!");
                            break;
                        case 5:
                            Debug.WriteLine("Swipe Up 5!");
                            break;
                    }
                    swipe.upMotion = 0;
                }
                else
                {
                    switch (numFingers)
                    {
                        case 1:
                            Debug.WriteLine("Swipe Down 1!");
                            break;
                        case 2:
                            Debug.WriteLine("Swipe Down 2!");
                            break;
                        case 5:
                            Debug.WriteLine("Swipe Down 5!");
                            break;
                    }
                    swipe.downMotion = 0;
                }

                // Turn off gesture and set cooldown timer to .5 sec
                swipe.gestureOn = false;
                swipe.gestureFinished = true;
                System.Timers.Timer timer = new System.Timers.Timer();
                timer.Elapsed += new ElapsedEventHandler(SwipeSetFinished);
                timer.Interval = 500;
                timer.Start();
            }
        }

        // Poke gesture
        void PokeGesture(Leap.Controller controller)
        {
            Leap.Frame frame = controller.Frame();
            Hand hand = frame.Hands[0];

            // Initiate Poke gesture if Index finger is extended
            if (!poke.gestureOn)
            {
                foreach (Finger finger in hand.Fingers)
                {
                    if (finger.Type == Finger.FingerType.TYPE_INDEX && ((Pointable)finger).IsExtended)
                    {
                        Debug.WriteLine("Poke Started");
                        poke.gestureOn = true;
                    }
                }
            }

            // Perform Poke gesture if Index finger moves forward & back
            if (poke.gestureOn)
            {
                Leap.Vector motion = frame.Translation(controller.Frame(1));

                if (motion.y < -5.0f)
                {
                    poke.forwardMotion++;
                }
                else if (motion.y > 5.0f)
                {
                    poke.backwardMotion++;
                }

                // Turn off gesture and set cooldown timer to .5 sec
                if (poke.forwardMotion > 5 && poke.backwardMotion > 5)
                {
                    Debug.WriteLine("Poke!");
                    poke.forwardMotion = 0;
                    poke.backwardMotion = 0;
                    poke.gestureOn = false;
                    poke.gestureFinished = true;
                    System.Timers.Timer timer = new System.Timers.Timer();
                    timer.Elapsed += new ElapsedEventHandler(PokeSetFinished);
                    timer.Interval = 500;
                    timer.Start();
                }
            }
        }

        // Cooldown for Swipe gesture
        void SwipeSetFinished(object sender, ElapsedEventArgs e)
        {
            swipe.gestureFinished = false;
            System.Timers.Timer timer = sender as System.Timers.Timer;
            timer.Elapsed -= new ElapsedEventHandler(SwipeSetFinished);
            timer.Stop();
        }

        // Cooldown for Poke gesture
        void PokeSetFinished(object sender, ElapsedEventArgs e)
        {
            poke.gestureFinished = false;
            System.Timers.Timer timer = sender as System.Timers.Timer;
            timer.Elapsed -= new ElapsedEventHandler(PokeSetFinished);
            timer.Stop();
        }

        // Return number of fingers extended in given hand
        int GetNumFingersExtended(Hand hand)
        {
            int numFingersExtended = 0;
            foreach (Pointable pointable in hand.Pointables)
            {
                if (pointable.IsExtended)
                {
                    numFingersExtended++;
                }
            }
            return numFingersExtended;
        }
    }

    public interface ILeapEventDelegate
    {
        void LeapEventNotification(string EventName);
    }

    // Trigger events
    public class LeapEventListener : Listener
    {
        readonly ILeapEventDelegate eventDelegate;

        public LeapEventListener(ILeapEventDelegate delegateObject)
        {
            this.eventDelegate = delegateObject;
        }
        public override void OnInit(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onInit");
        }
        public override void OnConnect(Controller controller)
        {
            controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
            this.eventDelegate.LeapEventNotification("onConnect");
        }

        public override void OnFrame(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onFrame");
        }
        public override void OnExit(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onExit");
        }
        public override void OnDisconnect(Controller controller)
        {
            this.eventDelegate.LeapEventNotification("onDisconnect");
        }

    }

}
