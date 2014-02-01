﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PointyStickBlend
{
    /// <summary>
    /// Interaction logic for RunWindow.xaml
    /// </summary>
    public partial class RunWindow : Window
    {
        [DllImport("Kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll")]
        static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll")]
        static extern bool ResetEvent(IntPtr hEvent);

        bool events_initialized = false;

        IntPtr event_monitoring = IntPtr.Zero;
        IntPtr event_snapshot = IntPtr.Zero;

        Process instrumented_process = new Process();

        public RunWindow()
        {
            InitializeComponent();
            
            /* Set up our system events */
            events_initialized = false;
            initialize_events();

            /* Start the status timer */
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(status_timer_tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        ~RunWindow()
        {
            clean_events();
        }

        private void clean_events()
        {
            CloseHandle(event_monitoring);
            CloseHandle(event_snapshot);
        }

        private void initialize_events()
        {
            if (events_initialized)
                return;

            uint EVENT_MODIFY_STATE = 0x0002;
            event_monitoring = OpenEvent(EVENT_MODIFY_STATE, false, "MONITORING");
            if(event_monitoring == IntPtr.Zero)
            {
                // NULL, so we need to make the event
                event_monitoring = CreateEvent(IntPtr.Zero, true, false, "MONITORING");
            }

            event_snapshot = OpenEvent(EVENT_MODIFY_STATE, false, "SNAPSHOT");    
            if(event_snapshot == IntPtr.Zero)
            {
                event_snapshot = CreateEvent(IntPtr.Zero, true, false, "SNAPSHOT");
            }

            events_initialized = true;
        }

        private void open_application(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            if(dialog.ShowDialog() == true)
            {
                textbox_filename.Text = dialog.FileName;
            }
        }

        private void start_collection(object sender, RoutedEventArgs e)
        {
            string command_string = "-t PointyStickPinTool ";

            /*
             * Tracing Settings
             */
            if(tracing_checkbox.IsChecked == false)
            {
                command_string += "-notrace ";
            }
            else
            {
                if(tracing_fromstart.IsChecked == true)
                {
                    enable_tracing();
                }
            }

            /*
             * Region Monitoring Settings
             */

            /*
             * Append the application name
             */
            command_string += " -- " + textbox_filename.Text;

            /*
             * Start the PIN tool
             */
            try
            {
                string pin_root = System.Environment.GetEnvironmentVariable("PIN_ROOT");

                if (pin_root.CompareTo("") == 0)
                {
                    MessageBox.Show("Please define the environment variable PIN_ROOT and restart the application.");
                }
                else
                {
                    instrumented_process.StartInfo.FileName = pin_root + "\\pin.exe";
                    instrumented_process.StartInfo.Arguments = command_string;
                    instrumented_process.EnableRaisingEvents = true;
                    instrumented_process.Exited += new EventHandler(instrumented_exited);
                    instrumented_process.Start();

                    if (instrumented_process == null)
                    {
                        throw new Exception("Couldn't start pin.exe");
                    }
                    
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void instrumented_exited(object o, EventArgs e)
        {
            switch (instrumented_process.ExitCode)
            {
                case -1:
                    MessageBox.Show("Couldn't locate target application.");
                    break;
                case 0:
                    break;
                case 1:
                    MessageBox.Show("Invalid PIN tool parameters.");
                    break;
                default:
                    MessageBox.Show("Unhandled PIN error code of " + instrumented_process.ExitCode + ".");
                    break;
            }
            
        }

        private bool enable_tracing()
        {
            // set the monitoring event
            SetEvent(event_monitoring);

            return true;
        }

        private bool disable_tracing()
        {
            // disable the monitoring event
            ResetEvent(event_monitoring);

            return true;
        }

        private void enable_tracing_button_Click(object sender, RoutedEventArgs e)
        {
            enable_tracing();
        }

        private void disable_tracing_button_Click(object sender, RoutedEventArgs e)
        {
            disable_tracing();
        }

        private void status_timer_tick(object sender, EventArgs e)
        {
            int WAIT_OBJECT_0 = 0;

            uint status = WaitForSingleObject(event_monitoring, 0);

            if(status == WAIT_OBJECT_0) // signaled
            {
                tracing_status_label.Content = "Enabled";
            }
            else
            {
                tracing_status_label.Content = "Disabled";
            }

            status = WaitForSingleObject(event_snapshot, 0);
            if(status == WAIT_OBJECT_0)
            {
                snapshot_status_label.Content = "Enabled";
            }
            else
            {
                snapshot_status_label.Content = "Disabled";
            }
        }
    }
}
