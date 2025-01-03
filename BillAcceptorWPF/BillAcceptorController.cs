using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace BillAcceptorWPF
{
    public class BillAcceptorController
    {
        // Events
        public event EventHandler<string>? StateChanged;
        public event EventHandler<int>? BillAccepted;
        public event EventHandler<string>? LogMessage;

        // Constants
        private const int BAUDRATE = 9600;
        private const int RESPONSE_SIZE = 254;
        private const int MESSAGE_SIZE = 254;
        private const int COMMANDS_SIZE = 50;
        private const int TIMEOUT = 5000;

        // Estados definidos
        private enum AcceptorState
        {
            STATUS = 0,
            STACK = 1,
            SEND_ACK = 2,
            GET_DATA = 3,
            INITIALIZE = 4,
            RESET = 5,
            FATAL_ERROR = 6,
            VEND_VALID = 7,
            WAITING_FOR_COMMAND = 8
        }

        // Valores de respuesta de estado
        private const byte ENABLE = 0x11;
        private const byte ACCEPTING = 0x12;
        private const byte ESCROW = 0x13;
        private const byte STACKING = 0x14;
        private const byte VEND_VALID = 0x15;
        private const byte STACKED = 0x16;
        private const byte REJECTING = 0x17;
        private const byte RETURNING = 0x18;
        private const byte HOLDING = 0x19;
        private const byte DISABLE = 0x1A;
        private const byte INITIALIZE = 0x1B;

        // Valores de estado de error
        private const byte STACKER_FULL = 0x43;
        private const byte STACKER_OPEN = 0x44;
        private const byte JAM_IN_ACCEPTOR = 0x45;
        private const byte JAM_IN_STACKER = 0x46;
        private const byte PAUSE = 0x47;
        private const byte CHEATED = 0x48;
        private const byte FAILURE = 0x49;
        private const byte COMMUNICATION_ERROR = 0x4A;

        // Valores de estado de encendido
        private const byte POWER_UP = 0x40;
        private const byte POWER_UP_WITH_BILL_IN_ACCEPTOR = 0x41;
        private const byte ENABLE_UP_WITH_BILL_IN_STACKER = 0x42;

        // Valores de protocolo
        private const byte ENQ = 0x05;
        private const byte ACK = 0x06;
        private const byte NAK = 0x15;
        private const byte INVALID_COMMAND = 0x15;

        // Valores de denominación
        private const byte ENABLE_DENOMINATION = 0x30;
        private const byte SECURITY = 0x31;
        private const byte COMMUNICATION_MODE = 0x32;
        private const byte INHIBIT = 0x33;
        private const byte DIRECTION = 0x34;
        private const byte OPTIONAL_FUNCTION = 0x35;

        // Valores de configuración
        private const byte SET_ENABLE_DENOMINATION = 0x36;
        private const byte SET_SECURITY = 0x37;
        private const byte SET_COMMUNICATION_MODE = 0x38;
        private const byte SET_INHIBIT = 0x39;
        private const byte SET_DIRECTION = 0x3A;
        private const byte SET_OPTIONAL_FUNCTION = 0x3B;
        private const byte SET_VERSION_INFORMATION = 0x3C;
        private const byte SET_BOOT_VERSION_INFORMATION = 0x3D;
        private const byte SET_DENOMINATION_DATA = 0x3E;

        // Valores específicos del TBV-100
        private const byte STX = 0x02;
        private const byte ETX = 0x03;

        // Campos privados
        private SerialPort? serialPort;
        private AcceptorState state = AcceptorState.WAITING_FOR_COMMAND;
        private AcceptorState lastState = AcceptorState.WAITING_FOR_COMMAND;
        private bool acceptFlag = false;
        private bool flagBills = false;
        private int unMoney = 0;
        private byte[] commands = new byte[COMMANDS_SIZE];
        private byte[] response = new byte[RESPONSE_SIZE];
        private byte[] message = new byte[MESSAGE_SIZE];
        private byte[] statusResponseTable = new byte[70];
        private byte[] flagForResponseAction = new byte[70];
        private string[] errorMessages = new string[12];

        public BillAcceptorController()
        {
            SetPredefinedConfiguration();
            StatusCheckerFilling();
            ErrorMessageFilling();
        }

        public async Task Connect(string portName)
        {
            try
            {
                serialPort = new SerialPort(portName, BAUDRATE, Parity.Even, 7, StopBits.One);
                serialPort.ReadTimeout = TIMEOUT;
                serialPort.WriteTimeout = TIMEOUT;
                serialPort.Handshake = Handshake.None;
                serialPort.DtrEnable = true;
                serialPort.RtsEnable = true;
                serialPort.ReadBufferSize = 4096;
                serialPort.WriteBufferSize = 4096;
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                state = AcceptorState.INITIALIZE;
                await ProcessState();
                LogMessage?.Invoke(this, "Conectado al puerto " + portName);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error conectando: " + ex.Message);
                throw;
            }
        }

        public async Task<string> AutoDetectPort()
        {
            string[] availablePorts = SerialPort.GetPortNames();
            LogMessage?.Invoke(this, "Buscando aceptador de billetes TBV-100...");
            LogMessage?.Invoke(this, $"Puertos disponibles: {string.Join(", ", availablePorts)}");

            foreach (string port in availablePorts)
            {
                LogMessage?.Invoke(this, $"Probando puerto {port}...");
                try
                {
                    using (var testPort = new SerialPort(port, BAUDRATE, Parity.Even, 7, StopBits.One))
                    {
                        testPort.ReadTimeout = 3000;
                        testPort.WriteTimeout = 3000;
                        testPort.Handshake = Handshake.None;
                        testPort.DtrEnable = true;
                        testPort.RtsEnable = true;

                        LogMessage?.Invoke(this, $"Abriendo puerto {port}...");

                        // Close port if it's already open
                        if (testPort.IsOpen)
                        {
                            testPort.Close();
                            await Task.Delay(1000);
                        }

                        testPort.Open();
                        await Task.Delay(1000);

                        // Enviar comando de status TBV-100
                        byte[] testCmd = new byte[7];
                        testCmd[0] = STX;  // STX
                        testCmd[1] = 0x08;  // Longitud
                        testCmd[2] = 0x10;  // Status
                        testCmd[3] = 0x00;
                        testCmd[4] = 0x00;
                        testCmd[5] = ETX;   // ETX
                        testCmd[6] = CalculateBCC(testCmd, 1, 5);

                        LogMessage?.Invoke(this, $"Enviando comando de prueba a {port}...");
                        testPort.DiscardOutBuffer();
                        testPort.DiscardInBuffer();
                        await Task.Delay(200);
                        testPort.Write(testCmd, 0, testCmd.Length);
                        await Task.Delay(1000);

                        if (testPort.BytesToRead > 0)
                        {
                            byte[] buffer = new byte[20];
                            int bytesRead = testPort.Read(buffer, 0, buffer.Length);

                            if (bytesRead > 0 && buffer[0] == STX)  // STX
                            {
                                LogMessage?.Invoke(this, $"¡TBV-100 encontrado en {port}!");
                                return port;
                            }
                        }

                        testPort.Close();
                        await Task.Delay(500);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    LogMessage?.Invoke(this, $"Error: Puerto {port} está siendo usado por otra aplicación");
                    continue;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Error probando {port}: {ex.Message}");
                    continue;
                }
            }

            LogMessage?.Invoke(this, "No se encontró el TBV-100 en ningún puerto.");
            throw new Exception("No se encontró el TBV-100.");
        }

        public async Task Initialize()
        {
            try
            {
                // Primero detectamos el puerto
                string portName = await AutoDetectPort();

                // Luego conectamos
                await Connect(portName);

                // Finalmente inicializamos
                state = AcceptorState.INITIALIZE;
                await ProcessState();
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error en inicialización: " + ex.Message);
                throw;
            }
        }

        private async Task ProcessState()
        {
            switch (state)
            {
                case AcceptorState.STATUS:
                    await StateAcceptanceStatus();
                    break;
                case AcceptorState.STACK:
                    await StateAcceptanceStack();
                    break;
                case AcceptorState.SEND_ACK:
                    await StateAcceptanceSendAck();
                    break;
                case AcceptorState.GET_DATA:
                    await StateAcceptanceGetData();
                    break;
                case AcceptorState.INITIALIZE:
                    await StateAcceptanceInitialize();
                    break;
                case AcceptorState.RESET:
                    await StateAcceptanceReset();
                    break;
                case AcceptorState.FATAL_ERROR:
                    await StateAcceptanceFatalError();
                    break;
                case AcceptorState.VEND_VALID:
                    await StateAcceptanceVendValid();
                    break;
                case AcceptorState.WAITING_FOR_COMMAND:
                    await StateWaitingForCommand();
                    break;
            }
        }

        private async Task StateAcceptanceStatus()
        {
            LogMessage?.Invoke(this, "Inside status");
            CommandStatus();
            await SendCmd(commands, commands[1]);
            await ReadResponse();

            for (int k = 0; k < 22; k++)
            {
                if (response[2] == statusResponseTable[k])
                {
                    break;
                }
            }

            // Solo procesamos el estado si ha cambiado
            if (state != AcceptorState.STATUS)
            {
                state = AcceptorState.STATUS;
                await ProcessState();
            }
            else
            {
                await Task.Delay(100);
            }
        }

        private async Task StateAcceptanceStack()
        {
            LogMessage?.Invoke(this, "Inside stack");
            for (int i = 62; i < 69; i++)
            {
                if (response[3] == statusResponseTable[i])
                {
                    unMoney = GetBillValue(i - 62);
                    await Task.Delay(5);
                }
            }

            CommandStack1();
            await SendCmd(commands, commands[1]);
            await Task.Delay(100);
            await ReadResponse();
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceSendAck()
        {
            LogMessage?.Invoke(this, "Inside send ACK");
            CommandAck();
            await SendCmd(commands, commands[1]);
            await Task.Delay(100);
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceInitialize()
        {
            LogMessage?.Invoke(this, "Inside initialize");
            CommandEnable();
            await SendCmd(commands, commands[1]);
            await Task.Delay(100);
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceGetData()
        {
            LogMessage?.Invoke(this, "Inside get data");
            for (int i = 41; i < 52; i++)
            {
                if (response[3] == statusResponseTable[i])
                {
                    string errorMessage = errorMessages[i - 41];
                    LogMessage?.Invoke(this, $"Error: {errorMessage}");
                }
            }
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceReset()
        {
            LogMessage?.Invoke(this, "Inside Reset");
            CommandReset();
            await SendCmd(commands, commands[1]);
            await Task.Delay(100);
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceFatalError()
        {
            LogMessage?.Invoke(this, "Inside Fatal Error");
            for (int i = 52; i < 62; i++)
            {
                if (response[3] == statusResponseTable[i])
                {
                    string errorMessage = errorMessages[i - 29];
                    LogMessage?.Invoke(this, $"Fatal Error: {errorMessage}");
                }
            }
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateAcceptanceVendValid()
        {
            LogMessage?.Invoke(this, "Inside Vend Valid");
            BillAccepted?.Invoke(this, unMoney);
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        private async Task StateWaitingForCommand()
        {
            LogMessage?.Invoke(this, "Waiting for command");
            await Task.Delay(100);
        }

        private void CommandStatus()
        {
            commands[0] = STX;  // STX = 0x02
            commands[1] = 0x08;  // Longitud
            commands[2] = 0x10;  // Comando Status para TBV-100
            commands[3] = 0x00;
            commands[4] = 0x00;
            commands[5] = ETX;   // ETX = 0x03
            byte bcc = CalculateBCC(commands, 1, 5);
            commands[6] = bcc;
        }

        private void CommandStack1()
        {
            commands[0] = 0x02;
            commands[1] = 0x03;
            commands[2] = 0x06;
            commands[3] = 0x30;
            commands[4] = 0x42;
            commands[5] = 0x03;
        }

        private void CommandAck()
        {
            commands[0] = 0x02;
            commands[1] = 0x03;
            commands[2] = 0x06;
            commands[3] = 0x30;
            commands[4] = 0x43;
            commands[5] = 0x03;
        }

        private void CommandEnable()
        {
            commands[0] = STX;  // STX = 0x02
            commands[1] = 0x08;  // Longitud
            commands[2] = 0x20;  // Comando Enable para TBV-100
            commands[3] = 0x00;
            commands[4] = 0x00;
            commands[5] = ETX;   // ETX = 0x03
            byte bcc = CalculateBCC(commands, 1, 5);
            commands[6] = bcc;
        }

        private void CommandReset()
        {
            commands[0] = STX;  // STX = 0x02
            commands[1] = 0x08;  // Longitud
            commands[2] = 0x40;  // Comando Reset para TBV-100
            commands[3] = 0x00;
            commands[4] = 0x00;
            commands[5] = ETX;   // ETX = 0x03
            byte bcc = CalculateBCC(commands, 1, 5);
            commands[6] = bcc;
        }

        private async Task SendCmd(byte[] cmd, int len)
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            try
            {
                serialPort.DiscardOutBuffer();
                await Task.Delay(50);
                serialPort.Write(cmd, 0, len);
                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error enviando comando: " + ex.Message);
                throw;
            }
        }

        private async Task ReadResponse()
        {
            if (serialPort == null || !serialPort.IsOpen) return;

            try
            {
                serialPort.DiscardInBuffer();

                // Increased retries and timeout
                int retries = 20;
                while (retries > 0 && serialPort.BytesToRead == 0)
                {
                    await Task.Delay(200);
                    retries--;
                }

                if (serialPort.BytesToRead > 0)
                {
                    await Task.Delay(200);
                    int bytesRead = serialPort.Read(response, 0, response.Length);
                    if (bytesRead > 0)
                    {
                        ProcessResponse();
                    }
                }
                else
                {
                    LogMessage?.Invoke(this, "No se recibió respuesta del dispositivo");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error leyendo respuesta: " + ex.Message);
                throw;
            }
        }

        private void ProcessResponse()
        {
            if (response[2] == ESCROW)
            {
                BillAccepted?.Invoke(this, unMoney);
                LogMessage?.Invoke(this, $"Billete detectado: ${unMoney}");
            }
            else if (response[2] >= STACKER_FULL && response[2] <= COMMUNICATION_ERROR)
            {
                string errorMessage = GetErrorMessage(response[2]);
                LogMessage?.Invoke(this, "Error: " + errorMessage);
                StateChanged?.Invoke(this, "Error: " + errorMessage);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // Solo procesamos si hay datos disponibles
                if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
                {
                    ReadResponse().Wait();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error en DataReceived: " + ex.Message);
            }
        }

        // Métodos públicos para control desde la UI
        public async Task StartAccepting()
        {
            acceptFlag = true;
            state = AcceptorState.STATUS;
            await ProcessState();
        }

        public async Task StopAccepting()
        {
            try
            {
                acceptFlag = false;
                state = AcceptorState.WAITING_FOR_COMMAND;
                LogMessage?.Invoke(this, "Aceptador detenido");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error al detener: " + ex.Message);
                throw;
            }
        }

        public async Task Disconnect()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    await StopAccepting();
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                    LogMessage?.Invoke(this, "Desconectado");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, "Error al desconectar: " + ex.Message);
                throw;
            }
        }

        private void StatusCheckerFilling()
        {
            statusResponseTable[0] = ENABLE; flagForResponseAction[0] = (byte)AcceptorState.STATUS;
            statusResponseTable[1] = ACCEPTING; flagForResponseAction[1] = (byte)AcceptorState.STATUS;
            statusResponseTable[2] = ESCROW; flagForResponseAction[2] = (byte)AcceptorState.STACK;
            statusResponseTable[3] = STACKING; flagForResponseAction[3] = (byte)AcceptorState.STATUS;
            statusResponseTable[4] = VEND_VALID; flagForResponseAction[4] = (byte)AcceptorState.VEND_VALID;
            statusResponseTable[5] = STACKED; flagForResponseAction[5] = (byte)AcceptorState.STATUS;
            statusResponseTable[6] = REJECTING; flagForResponseAction[6] = (byte)AcceptorState.GET_DATA;
            statusResponseTable[7] = RETURNING; flagForResponseAction[7] = (byte)AcceptorState.STATUS;
            statusResponseTable[8] = HOLDING; flagForResponseAction[8] = (byte)AcceptorState.FATAL_ERROR;
            statusResponseTable[9] = DISABLE; flagForResponseAction[9] = (byte)AcceptorState.WAITING_FOR_COMMAND;
            statusResponseTable[10] = INITIALIZE; flagForResponseAction[10] = (byte)AcceptorState.INITIALIZE;

            statusResponseTable[11] = 0x40; flagForResponseAction[11] = (byte)AcceptorState.RESET;
            statusResponseTable[12] = 0x41; flagForResponseAction[12] = (byte)AcceptorState.RESET;
            statusResponseTable[13] = 0x42; flagForResponseAction[13] = (byte)AcceptorState.RESET;

            statusResponseTable[14] = STACKER_FULL; flagForResponseAction[14] = (byte)AcceptorState.STATUS;
            statusResponseTable[15] = STACKER_OPEN; flagForResponseAction[15] = (byte)AcceptorState.STATUS;
            statusResponseTable[16] = JAM_IN_ACCEPTOR; flagForResponseAction[16] = (byte)AcceptorState.STATUS;
            statusResponseTable[17] = JAM_IN_STACKER; flagForResponseAction[17] = (byte)AcceptorState.STATUS;
            statusResponseTable[18] = PAUSE; flagForResponseAction[18] = (byte)AcceptorState.STATUS;
            statusResponseTable[19] = CHEATED; flagForResponseAction[19] = (byte)AcceptorState.STATUS;
            statusResponseTable[20] = FAILURE; flagForResponseAction[20] = (byte)AcceptorState.FATAL_ERROR;
            statusResponseTable[21] = COMMUNICATION_ERROR; flagForResponseAction[21] = (byte)AcceptorState.STATUS;

            // Códigos adicionales del protocolo
            statusResponseTable[22] = 0x05; // ENQ
            flagForResponseAction[22] = (byte)AcceptorState.STATUS;

            statusResponseTable[23] = 0x06; // ACK
            flagForResponseAction[23] = (byte)AcceptorState.STATUS;

            statusResponseTable[24] = 0x15; // INVALID_COMMAND
            flagForResponseAction[24] = (byte)AcceptorState.FATAL_ERROR;

            // Códigos de denominación y funciones
            for (int i = 25; i <= 39; i++)
            {
                statusResponseTable[i] = (byte)(0x30 + (i - 25));  // Códigos desde 0x30 hasta 0x44
                flagForResponseAction[i] = (byte)AcceptorState.STATUS;
            }

            // Códigos de error
            for (int i = 41; i <= 51; i++)
            {
                statusResponseTable[i] = (byte)(0x50 + (i - 41));  // Códigos desde 0x50 hasta 0x5A
                flagForResponseAction[i] = (byte)AcceptorState.STATUS;
            }

            // Errores fatales
            for (int i = 52; i <= 61; i++)
            {
                statusResponseTable[i] = (byte)(0x60 + (i - 52));  // Códigos desde 0x60 hasta 0x69
                flagForResponseAction[i] = (byte)AcceptorState.FATAL_ERROR;
            }
        }

        private void ErrorMessageFilling()
        {
            errorMessages[0] = "STACKER_FULL";
            errorMessages[1] = "STACKER_OPEN";
            errorMessages[2] = "JAM_IN_ACCEPTOR";
            errorMessages[3] = "JAM_IN_STACKER";
            errorMessages[4] = "PAUSE";
            errorMessages[5] = "CHEATED";
            errorMessages[6] = "FAILURE";
            errorMessages[7] = "COMMUNICATION_ERROR";
            errorMessages[8] = "INVALID_COMMAND";
            errorMessages[9] = "STACK_MOTOR_FAILURE";
            errorMessages[10] = "TRANSPORT_MOTOR_FAILURE";
            errorMessages[11] = "VALIDATOR_HEAD_REMOVE";
        }

        private void SetPredefinedConfiguration()
        {
            // Configuración predefinida como en el Arduino
        }

        private int GetBillValue(int index)
        {
            // Valores de billetes según el índice
            int[] billValues = { 1000, 2000, 5000, 10000, 20000, 50000 };
            return index < billValues.Length ? billValues[index] : 0;
        }

        private string GetErrorMessage(byte errorCode)
        {
            return errorCode switch
            {
                STACKER_FULL => "Stacker lleno",
                STACKER_OPEN => "Stacker abierto",
                JAM_IN_ACCEPTOR => "Atasco en aceptador",
                JAM_IN_STACKER => "Atasco en stacker",
                PAUSE => "Pausado",
                CHEATED => "Intento de fraude",
                FAILURE => "Fallo",
                COMMUNICATION_ERROR => "Error de comunicación",
                _ => "Error desconocido"
            };
        }

        private byte CalculateBCC(byte[] data, int start, int length)
        {
            byte bcc = 0;
            for (int i = start; i <= length; i++)
            {
                bcc ^= data[i];
            }
            return bcc;
        }
    }
}
