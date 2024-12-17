using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BillAcceptorWPF
{
    public partial class MainWindow : Window
    {
        private readonly BillAcceptorController billAcceptor = new();
        private readonly DispatcherTimer statusTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        public MainWindow()
        {
            InitializeComponent();
            LoadComPorts();
            InitializeTimer();
            statusTimer.Tick += StatusTimer_Tick;
            billAcceptor.StateChanged += BillAcceptor_StateChanged;
            billAcceptor.BillAccepted += BillAcceptor_BillAccepted;
            billAcceptor.LogMessage += BillAcceptor_LogMessage;
        }

        private void InitializeTimer()
        {
        }

        private void LoadComPorts()
        {
            ComPortComboBox.Items.Clear();
            string[] availablePorts = SerialPort.GetPortNames();
            
            if (availablePorts.Length == 0)
            {
                StatusBarTextBlock.Text = "No se detectaron puertos COM";
                LogTextBox.AppendText("Advertencia: No se encontraron puertos COM disponibles\n");
                return;
            }

            foreach (string port in availablePorts)
            {
                ComPortComboBox.Items.Add(port);
                if (port == "COM6")
                {
                    ComPortComboBox.SelectedItem = port;
                    StatusBarTextBlock.Text = "TBV-100 detectado en " + port;
                }
            }

            if (ComPortComboBox.SelectedItem == null && ComPortComboBox.Items.Count > 0)
            {
                ComPortComboBox.SelectedIndex = 0;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPort = ComPortComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedPort))
                {
                    MessageBox.Show("Por favor selecciona un puerto COM");
                    return;
                }

                StatusBarTextBlock.Text = "Intentando conectar a " + selectedPort + "...";
                await billAcceptor.Connect(selectedPort);
                
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                InitializeButton.IsEnabled = true;
                statusTimer.Start();
                
                StatusBarTextBlock.Text = "Conectado a " + selectedPort;
                LogTextBox.AppendText($"Conectado exitosamente a {selectedPort}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar: " + ex.Message);
                StatusBarTextBlock.Text = "Error de conexión";
                LogTextBox.AppendText($"Error de conexión: {ex.Message}\n");
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            await billAcceptor.Disconnect();
            
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            statusTimer.Stop();
            
            StateTextBlock.Text = "Disconnected";
            StatusBarTextBlock.Text = "Disconnected";
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await billAcceptor.StartAccepting();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await billAcceptor.StopAccepting();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private async void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await billAcceptor.Initialize();
                LogTextBox.AppendText("Aceptador de billetes inicializado correctamente.\n");
            }
            catch (Exception ex)
            {
                LogTextBox.AppendText($"Error: {ex.Message}\n");
            }
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            // Ya no necesitamos CheckStatus porque ahora usamos eventos
        }

        private void BillAcceptor_StateChanged(object? sender, string newState)
        {
            StateTextBlock.Text = newState;
        }

        private void BillAcceptor_BillAccepted(object? sender, int amount)
        {
            LogTextBox.AppendText($"Billete aceptado: ${amount}\n");
        }

        private void BillAcceptor_LogMessage(object? sender, string message)
        {
            LogTextBox.AppendText(message + "\n");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadComPorts();
            LogTextBox.AppendText("Lista de puertos actualizada\n");
        }
    }
}
