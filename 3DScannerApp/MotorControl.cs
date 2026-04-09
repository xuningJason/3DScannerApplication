using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Printing.IndexedProperties;

//using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using System.Net;
//using System.Net.Sockets;
using STTech.BytesIO.Core;
using STTech.BytesIO.Tcp;


namespace _3DScannerApp
{
    internal class MotorControl
    {
        #region 静态字段
        public MotorControl() { }
        private static readonly MotorControl instance;

        public static MotorControl Instance
        {
            get
            {
                return instance;
            }
        }
        static MotorControl()
        {
            instance = new MotorControl();
        }
        #endregion

        private TcpClient _tcpClient;

        private TcpServer server;


        private string serialPortName; // 串口号
        private int baudRate; // 波特率
        private SerialPort serialPort;
        Parity parity = Parity.None; // 校验位
        int dataBits = 8; // 数据位
        StopBits stopBits = StopBits.One; // 停止位

        public bool isReadStatus = false;

        private static Thread _sendThread;

        private static Thread _receiveThread;

        public float positionFlo;

        public float tempretureFlo;

        public float humityFlo;

        public float presureFlo;

        public int uartSendCount = 0;

        public bool isConnected = false;


        public void CreateMotorControl()
        {
            //_tcpClient = new TcpClient();

            //_tcpClient.Connect("192.168.1.200", 2000);

            //networkStream = _tcpClient.GetStream();
            //byte[] data = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            //networkStream.Write(data, 0, data.Length);

            int port = 8234;
            server = new TcpServer();
            server.Port = port;

            server.Started += Server_Started;
            server.Closed += Server_Closed;
            server.ClientConnected += Server_ClientConnected; ;
            server.ClientDisconnected += Server_ClientDisconnected;
            server.ClientConnectionAcceptedHandle = (s, e) =>
            {
                if (server.Clients.Count() < 2)
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"服务器已满，关闭客户端[{e.ClientSocket.RemoteEndPoint}]的连接");
                    return false;
                }
            };
            try
            {
                server.StartAsync();
            }
            catch (Exception ex) 
            {
                MessageBox.Show("端口被占用，请使用网络配置工具更换");
            }
            //return;

            //this.serialPortName = serialPortName;
            //this.baudRate = baudRate;

            //// 创建串口对象
            //serialPort = new SerialPort(serialPortName, baudRate, parity, dataBits, stopBits);

            //try
            //{
            //    // 打开串口
            //    serialPort.Open();
            //    MessageBox.Show("已连接串口" + serialPortName);

            //    //isReadStatus = true;

            //    startControl();
            //    serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);
            //    //// 创建接收线程
            //    //_receiveThread = new Thread(ReceiveThreadFunction);
            //    //_receiveThread.Start();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"通信错误: {ex.Message}");
            //}
        }

        private void Server_ClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            MessageBox.Show($"客户端[{e.Client.Host}:{e.Client.Port}]断开连接");
        }

        private void Server_ClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            isConnected = true;
            MessageBox.Show($"客户端[{e.Client.Host}:{e.Client.Port}]连接成功");
            startControl();
            e.Client.OnDataReceived += Client_OnDataReceived;
            //e.Client.UseHeartbeatTimeout(10000);
        }

        private void Client_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
            uartSendCount -= 1;
            byte[] byteTemp = e.Data;
            if (byteTemp.Length == 17 && byteTemp[0] == 0x3E && byteTemp[1] == 0x92 && byteTemp[2] == 1 && byteTemp[3] == 8)
            {
                uartSendCount = 0;
                // 解析响应帧
                // byte[] speedBytes = new byte[] { receivedBuffer[9], receivedBuffer[8] };
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                byte[] positionBytes = new byte[] { byteData[5], byteData[6], byteData[7], byteData[8] };
                positionFlo = ConvertToFloat4(positionBytes) / 100;
                byte[] tempBytes = new byte[] { byteData[9], byteData[10] };
                tempretureFlo = ConvertToFloat2(tempBytes);
                byte[] humityBytes = new byte[] { byteData[11], byteData[12] };
                humityFlo = ConvertToFloat2(humityBytes);
                byte[] presureBytes = new byte[] { byteData[13], byteData[14], byteData[15] };
                float preFlo = ConvertToFloat3(presureBytes);
                float presureVal = preFlo >= 8388608 ? (preFlo - 16777216) / 8388608 : preFlo / 8388608;
                //presureFlo = presureVal;
                //presureFlo = (float)(180 / 0.81 * (presureVal - 0.1) + 30);
                presureFlo = (1000 * presureVal - 50 - 100) / 100;
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x01 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, ForwardMoveFeedback)) // 接收到了点动命令的反馈
                {
                    isForwardMoveReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x00 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, StopMoveFeedback)) // 接收到了停止命令的反馈
                {
                    isStopMoveReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x03 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, CoiledTriggerFeedback)) // 接收到了连续触发模式命令的反馈
                {
                    isCoiledTriggerReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x02 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, CoiledUntriggerFeedback))
                {
                    isCoiledUntriggerReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x05 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, IntervalTriggerFeedback))
                {
                    isIntervalTriggerReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x07 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, speedPositionModeFeedback))
                {
                    isSpeedPositionMode = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x08 && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, SaveLimitFeedback))
                {
                    isSaveLimitReceived = true;
                }
            }
            else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x0A && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, interruptFeedback)) // 接收到了中断扫描命令的反馈
                {
                    isInterruptReceived = true;
                }
            } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x0B && byteTemp[3] == 0x01)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, laserOpenFeedback)) // 接收到了打开激光命令的反馈
                {
                    isLaserOpenReceived = true;
                }
            } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x0B && byteTemp[3] == 0x00)
            {
                uartSendCount = 0;
                byte[] byteData = new byte[17];
                for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                if (JudgeByteArray(byteData, laserCloseFeedback)) // 接收到了关闭激光命令的反馈
                {
                    isLaserCloseReceived = true;
                }
            }
        }

        private void Server_Started(object sender, EventArgs e)
        {
            Console.WriteLine("开始监听服务器连接");
        }

        private void Server_Closed(object sender, EventArgs e)
        {
            Console.WriteLine("停止监听服务器连接");
        }

        private void TcpMessageSend(byte[] requestFrame)
        {
            foreach(TcpClient client in server.Clients)
            {
                client.SendAsync(requestFrame);
            }
        }

        bool isSending = false;
        private void startControl()
        {
            isReadStatus = true;
            isSending = true;
            // 创建发送线程
            _sendThread = new Thread(SendThreadFunction);
            _sendThread.Start();
        }

        public void SerialClose()
        {
            isSending = false;
            if (server != null) 
            {
                server.CloseAsync();
            }
        }

        private void SendThreadFunction()
        {
            while (isSending)
            {
                if (isReadStatus)
                {
                    readStatus();
                    Thread.Sleep(300);
                }
            }
        }

        private void ReceiveThreadFunction()
        {
            serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);
        }

        #region 工具方法

        public static float ConvertToFloat2(byte[] byteArray)
        {
            if (byteArray.Length != 2)
            {
                throw new ArgumentException("Byte array must be exactly 2 bytes in length.");
            }
            ushort result = (ushort)((byteArray[0] << 8) | byteArray[1]);
            return result;
        }

        // 三位字节数组转int
        public static int ConvertToFloat3(byte[] byteArray)
        {
            // 扩展到 4 字节（填充额外的字节 0x00）
            byte[] extendedArray = new byte[4];
            Array.Copy(byteArray, 0, extendedArray, 1, byteArray.Length); // 从索引 1 开始填充

            // 将字节数组反转，以适应 BitConverter 的小端格式
            Array.Reverse(extendedArray);

            // 将 4 字节数组转换为 32 位整数
            int result = BitConverter.ToInt32(extendedArray, 0);
            return result;
        }

        public static float ConvertToFloat4(byte[] byteArray)
        {
            if (byteArray.Length != 4)
            {
                throw new ArgumentException("Byte array must be exactly 4 bytes in length.");
            }
            int result = BitConverter.ToInt32(byteArray, 0);
            return result;
        }

        // 获取和校验的结果
        private byte[] GetByteArray(int length, byte[] byArr)
        {
            byte[] byteArray = new byte[length];
            for (int i = 0; i < byArr.Length; i++)
            {
                byteArray[i] = byArr[i];
            }
            byteArray[byteArray.Length - 1] = ComputeChecksum(byArr);
            return byteArray;
        }

        static byte ComputeChecksum(byte[] data)
        {
            int sum = 0;

            // 累加所有字节
            foreach (byte b in data)
            {
                sum += b;
            }

            // 取结果的低字节
            byte checksum = (byte)(sum & 0xFF);

            byte lowByte = (byte)(checksum & 0xFF);

            return lowByte;
        }

        // 比较指令是否相等
        private bool JudgeByteArray(byte[] data1, byte[] data2)
        {
            bool result = true;
            if (data1.Length != data2.Length) result = false;
            else
            {
                for (int i = 0; i < data1.Length; i++)
                {
                    if (data1[i] != data2[i])
                    {
                        result = false;
                        break;
                    }
                }
            }
            return result;
        }

        #endregion


        #region 485接收事件

        byte[] RxBuffer = new byte[1000];
        int RxLength = 0;
        // 接收事件
        private static List<byte> receivedBuffer = new List<byte>();
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            byte[] responseFrame = new byte[serialPort.BytesToRead];
            serialPort.Read(responseFrame, 0, responseFrame.Length);
            byte[] byteTemp = new byte[1000];
            int tempLength = responseFrame.Length;
            responseFrame.CopyTo(RxBuffer, RxLength);
            RxLength += tempLength;
            uartSendCount -= 1;
            while(RxLength >= 17)
            {
                RxBuffer.CopyTo(byteTemp, 0);
                if (byteTemp[0] == 0x3E && byteTemp[1] == 0x92 && byteTemp[2] == 1 && byteTemp[3] == 8)
                {
                    uartSendCount = 0;
                    // 解析响应帧
                    // byte[] speedBytes = new byte[] { receivedBuffer[9], receivedBuffer[8] };
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    byte[] positionBytes = new byte[] { byteData[5], byteData[6], byteData[7], byteData[8] };
                    positionFlo = ConvertToFloat4(positionBytes);
                    byte[] tempBytes = new byte[] { byteData[9], byteData[10] };
                    tempretureFlo = ConvertToFloat2(tempBytes);
                    byte[] humityBytes = new byte[] { byteData[11], byteData[12] };
                    humityFlo = ConvertToFloat2(humityBytes);
                    byte[] presureBytes = new byte[] { byteData[13], byteData[14], byteData[15] };
                    float preFlo = ConvertToFloat3(presureBytes);
                    float presureVal = preFlo >= 8388608 ? (preFlo - 16777216) / 8388608 : preFlo / 8388608;
                    presureFlo = presureVal;
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x01 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, ForwardMoveFeedback)) // 接收到了点动命令的反馈
                    {
                        isForwardMoveReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x00 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, StopMoveFeedback)) // 接收到了停止命令的反馈
                    {
                        isStopMoveReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x03 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, CoiledTriggerFeedback)) // 接收到了连续触发模式命令的反馈
                    {
                        isCoiledTriggerReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x02 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, CoiledUntriggerFeedback)) 
                    {
                        isCoiledUntriggerReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x05 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, IntervalTriggerFeedback)) 
                    {
                        isIntervalTriggerReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x07 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, speedPositionModeFeedback)) 
                    {
                        isSpeedPositionMode = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x09 && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, SaveLimitFeedback)) 
                    {
                        isSaveLimitReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else if (byteTemp[0] == 0x03 && byteTemp[1] == 0x01 && byteTemp[2] == 0x0A && byteTemp[3] == 0x01)
                {
                    uartSendCount = 0;
                    byte[] byteData = new byte[17];
                    for (int i = 0; i < 17; i++) byteData[i] = byteTemp[i];
                    if (JudgeByteArray(byteData, interruptFeedback)) // 接收到了中断扫描命令的反馈
                    {
                        isInterruptReceived = true;
                    }
                    for (int i = 17; i < RxLength; i++) RxBuffer[i - 17] = RxBuffer[i];
                    RxLength -= 17;
                } else
                {
                    uartSendCount = 0;
                    for (int i = 1; i < RxLength; i++) RxBuffer[i - 1] = RxBuffer[i];
                    RxLength--;
                    continue;
                }
            }
            
        }

        #endregion


        #region 命令格式声明
        /// <summary>
        /// 发给下位机的指令位自定义格式
        /// 第一位为03, 第二位为自定义的id 01， 第三位为操作码，操作码定义 （00为停止运动； 01为速度模式；02为模式一的未触发模式； 03为模式一的触发模式；04为模式二的未触发模式；05为模式二的触发模式；06为查询电机状态；07为速度位置模式；08初始化（无效命令先暂定）；09保存限位；0A中断扫描）
        /// </summary>
        /// 

        #endregion

        #region 控制下位机

        // 中断扫描
        bool isInterruptReceived = false;
        byte[] interruptFeedback = new byte[] { 0x03, 0x01, 0x0A, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public void Interrupt_Scan()
        {
            isReadStatus = false;
            Motor_Stop(false);
            byte[] frame = new byte[] { 0x03, 0x01, 0x0A, 0x01 };
            byte[] requestFrame = GetByteArray(5, frame);
            TcpMessageSend(requestFrame);
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            int count = 0;
            while (count < 5 && !isInterruptReceived)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isInterruptReceived = false;
            isReadStatus = true;
        }


        // 打开激光器
        bool isLaserOpenReceived = false;
        byte[] laserOpenFeedback = new byte[] { 0x03, 0x01, 0x0B, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public void OpenLaser()
        {
            isReadStatus = false;
            byte[] frame = new byte[] { 0x03, 0x01, 0x0B, 0x01 };
            byte[] requestFrame = GetByteArray(5, frame);
            TcpMessageSend(requestFrame);
            int count = 0;
            while (count < 5 && !isLaserOpenReceived)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isLaserOpenReceived = false;
            isReadStatus = true;
        }

        // 关闭激光器
        bool isLaserCloseReceived = false;
        byte[] laserCloseFeedback = new byte[] { 0x03, 0x01, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public void CloseLaser()
        {
            isReadStatus = false;
            byte[] frame = new byte[] { 0x03, 0x01, 0x0B, 0x00 };
            byte[] requestFrame = GetByteArray(5, frame);
            TcpMessageSend(requestFrame);
            int count = 0;
            while (count < 5 && !isLaserCloseReceived)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isLaserCloseReceived = false;
            isReadStatus = true;
        }


        // 查询电机状态数据
        private void readStatus()
        {
            byte[] readStatus = new byte[] { 0x03, 0x01, 0x06, 0x01 };
            byte[] requestFrame = GetByteArray(5, readStatus);
            TcpMessageSend(requestFrame);
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            uartSendCount += 1;
            //if (uartSendCount > 30)
            //{
            //    MessageBox.Show("串口连接中断，请断电检查后尝试重新连接");
            //    uartSendCount = 0;
            //    isReadStatus = false;
            //    SerialClose();
            //    Environment.Exit(0);
            //}
        }


        // 调光
        bool isSaveLimitReceived = false;
        byte[] SaveLimitFeedback = new byte[] { 0x03, 0x01, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public void Save_Limit_To_ESP(int brightness)
        {
            isReadStatus = false;
            Thread.Sleep(100);
            int leftLimitNumber = brightness;
            string leftLimitHexString = leftLimitNumber.ToString("X").PadLeft(8, '0');
            byte[] leftLimitBytes = Enumerable.Range(0, leftLimitHexString.Length)
                               .Where(x => x % 2 == 0)
                               .Select(x => Convert.ToByte(leftLimitHexString.Substring(x, 2), 16))
                               .ToArray();
            
            byte[] by = new byte[] {0x03, 0x01, 0x08, leftLimitBytes[3], leftLimitBytes[2], leftLimitBytes[1], leftLimitBytes[0] };
            byte[] requestFrame = GetByteArray(8, by);
            TcpMessageSend(requestFrame);
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            int count = 0;
            while (!isSaveLimitReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isSaveLimitReceived = false;
            isReadStatus = true;
        }

        bool isForwardMoveReceived = false;
        byte[] ForwardMoveFeedback = new byte[] { 0x03, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        // 点动
        public void Forward_Move(int direction, int speed)
        {
            isReadStatus = false;
            int SpeedNumber = speed * 100 * direction;
            string speedHexString = SpeedNumber.ToString("X").PadLeft(8, '0');
            byte[] speedBytes = Enumerable.Range(0, speedHexString.Length)
                               .Where(x => x % 2 == 0)
                               .Select(x => Convert.ToByte(speedHexString.Substring(x, 2), 16))
                               .ToArray();
            byte[] byteArr = new byte[4];
            byteArr[0] = speedBytes[3];
            byteArr[1] = speedBytes[2];
            byteArr[2] = speedBytes[1];
            byteArr[3] = speedBytes[0];

            //int leftLimitNumber = int.Parse(Properties.Settings.Default.leftLimit) * 100;
            //string leftLimitHexString = leftLimitNumber.ToString("X").PadLeft(8, '0');
            //byte[] leftLimitBytes = Enumerable.Range(0, leftLimitHexString.Length)
            //                   .Where(x => x % 2 == 0)
            //                   .Select(x => Convert.ToByte(leftLimitHexString.Substring(x, 2), 16))
            //                   .ToArray();
            //int rightLimitNumber = int.Parse(Properties.Settings.Default.rightLimit) * 100;
            //string rightLimitHexString = rightLimitNumber.ToString("X").PadLeft(8, '0');
            //byte[] rightLimitBytes = Enumerable.Range(0, rightLimitHexString.Length)
            //                   .Where(x => x % 2 == 0)
            //                   .Select(x => Convert.ToByte(rightLimitHexString.Substring(x, 2), 16))
            //                   .ToArray();

            byte[] by = new byte[] { 0x03, 0x01, 0x01, byteArr[0], byteArr[1], byteArr[2], byteArr[3] };
            byte[] requestFrame = GetByteArray(8, by);
            TcpMessageSend(requestFrame);
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            int count = 0;
            while (!isForwardMoveReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isForwardMoveReceived = false;
            isReadStatus = true;
        }

        // 运动到指定位置
        bool isSpeedPositionMode = false;
        byte[] speedPositionModeFeedback = new byte[] { 0x03, 0x01, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public void Motor_To_Target(int speed, float angle)
        {
            isReadStatus = false;
            int SpeedNumber = speed * 100;
            string speedHexString = SpeedNumber.ToString("X").PadLeft(8, '0');
            byte[] speedBytes = Enumerable.Range(0, speedHexString.Length)
                               .Where(x => x % 2 == 0)
                               .Select(x => Convert.ToByte(speedHexString.Substring(x, 2), 16))
                               .ToArray();
            byte[] byteArr = new byte[4];
            byteArr[0] = speedBytes[3];
            byteArr[1] = speedBytes[2];
            byteArr[2] = speedBytes[1];
            byteArr[3] = speedBytes[0];
            int AngleNumber = (int)(angle * 100);
            string angleHexString = AngleNumber.ToString("X").PadLeft(8, '0');
            byte[] angleBytes = Enumerable.Range(0, angleHexString.Length)
                               .Where(x => x % 2 == 0)
                               .Select(x => Convert.ToByte(angleHexString.Substring(x, 2), 16))
                               .ToArray();
            byte[] byteAngleArr = new byte[4];
            byteAngleArr[0] = angleBytes[3];
            byteAngleArr[1] = angleBytes[2];
            byteAngleArr[2] = angleBytes[1];
            byteAngleArr[3] = angleBytes[0];
            byte[] leftFrame = new byte[] { 0x03, 0x01, 0x07, byteAngleArr[0], byteAngleArr[1], byteAngleArr[2], byteAngleArr[3], byteArr[0], byteArr[1], byteArr[2], byteArr[3] };
            byte[] requestFrame = GetByteArray(12, leftFrame);
            TcpMessageSend(requestFrame);
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            int count = 0;
            while (count < 5 && !isSpeedPositionMode)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isSpeedPositionMode = false;
            isReadStatus = true;
        }

        bool isStopMoveReceived = false;
        byte[] StopMoveFeedback = new byte[] { 0x03, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        // 停止运动
        public void Motor_Stop(bool isNeedRead)
        {
            isReadStatus = false;
            byte[] by = new byte[] { 0x03, 0x01, 0x00, 0x01 };
            byte[] requestFrame = GetByteArray(5, by);
            TcpMessageSend(requestFrame);
            int count = 0;
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            while (!isStopMoveReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isStopMoveReceived = false;
            isReadStatus = isNeedRead;
        }

        bool isCoiledTriggerReceived = false;
        byte[] CoiledTriggerFeedback = new byte[] { 0x03, 0x01, 0x03, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        /// <summary>
        /// 开始连续触发扫描
        ///  SpeedNumber：速度值，incrementNumber：终点-起点，startNumber：起点，circleNumber：扫描周期，stayNumber：保持时间
        /// </summary>
        /// <param name="SpeedNumber"></param>
        /// <param name="incrementNumber"></param>
        /// <param name="startNumber"></param>
        /// <param name="circleNumber"></param>
        /// <param name="stayNumber"></param>
        public void Start_Coiled_Trigger(int SpeedNumber, int incrementNumber, int startNumber, int circleNumber, int stayNumber, int endNumber)
        {
            isReadStatus = false;
            string speedHexString = SpeedNumber.ToString("X").PadLeft(8, '0');
            byte[] speedBytes = Enumerable.Range(0, speedHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(speedHexString.Substring(x, 2), 16))
                                .ToArray();
            string incrementHexString = incrementNumber.ToString("X").PadLeft(8, '0');
            byte[] incrementBytes = Enumerable.Range(0, incrementHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(incrementHexString.Substring(x, 2), 16))
                                .ToArray();
            string startHexString = startNumber.ToString("X").PadLeft(8, '0');
            byte[] startBytes = Enumerable.Range(0, startHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(startHexString.Substring(x, 2), 16))
                                .ToArray();
            string circleHexString = circleNumber.ToString("X").PadLeft(8, '0');
            byte[] circleBytes = Enumerable.Range(0, circleHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(circleHexString.Substring(x, 2), 16))
                                .ToArray();
            string stayHexString = stayNumber.ToString("X").PadLeft(8, '0');
            byte[] stayBytes = Enumerable.Range(0, stayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(stayHexString.Substring(x, 2), 16))
                                .ToArray();
            string endNumberHexString = endNumber.ToString("X").PadLeft(8, '0');
            byte[] endNumberBytes = Enumerable.Range(0, endNumberHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(endNumberHexString.Substring(x, 2), 16))
                                .ToArray();
            // 03 01 03 增量的4位 速度的4位 周期的4位 保持的4位  开始位置的4位
            byte[] leftBytes = new byte[] { 0x03, 0x01, 0x03, incrementBytes[3], incrementBytes[2], incrementBytes[1], incrementBytes[0], speedBytes[3], speedBytes[2], speedBytes[1], speedBytes[0], circleBytes[3], circleBytes[2], circleBytes[1], circleBytes[0], stayBytes[3], stayBytes[2], stayBytes[1], stayBytes[0], startBytes[3], startBytes[2], startBytes[1], startBytes[0], endNumberBytes[3], endNumberBytes[2], endNumberBytes[1], endNumberBytes[0] };
            byte[] requestFrame = GetByteArray(28, leftBytes);
            TcpMessageSend(requestFrame);
            int count = 0;
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            while (!isCoiledTriggerReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isCoiledTriggerReceived = false;
            isReadStatus = true;
        }


        bool isCoiledUntriggerReceived = false;
        byte[] CoiledUntriggerFeedback = new byte[] {0x03, 0x01, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        /// <summary>
        /// 开始连续未触发模式
        /// SpeedNumber：速度值，incrementNumber：终点-起点，startNumber：起点，circleNumber：扫描周期，stayNumber：保持时间
        /// </summary>
        /// <param name="SpeedNumber"></param>
        /// <param name="incrementNumber"></param>
        /// <param name="startNumber"></param>
        /// <param name="circleNumber"></param>
        /// <param name="stayNumber"></param>
        public void Start_Coiled_Untrigger(int SpeedNumber, int incrementNumber, int startNumber, int circleNumber, int stayNumber, int endNumber)
        {
            isReadStatus = false;
            string speedHexString = SpeedNumber.ToString("X").PadLeft(8, '0');
            byte[] speedBytes = Enumerable.Range(0, speedHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(speedHexString.Substring(x, 2), 16))
                                .ToArray();
            string incrementHexString = incrementNumber.ToString("X").PadLeft(8, '0');
            byte[] incrementBytes = Enumerable.Range(0, incrementHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(incrementHexString.Substring(x, 2), 16))
                                .ToArray();
            string startHexString = startNumber.ToString("X").PadLeft(8, '0');
            byte[] startBytes = Enumerable.Range(0, startHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(startHexString.Substring(x, 2), 16))
                                .ToArray();
            string circleHexString = circleNumber.ToString("X").PadLeft(8, '0');
            byte[] circleBytes = Enumerable.Range(0, circleHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(circleHexString.Substring(x, 2), 16))
                                .ToArray();
            string stayHexString = stayNumber.ToString("X").PadLeft(8, '0');
            byte[] stayBytes = Enumerable.Range(0, stayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(stayHexString.Substring(x, 2), 16))
                                .ToArray();
            string endNumberHexString = endNumber.ToString("X").PadLeft(8, '0');
            byte[] endNumberBytes = Enumerable.Range(0, endNumberHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(endNumberHexString.Substring(x, 2), 16))
                                .ToArray();
            // 03 01 02 增量的4位 速度的4位 周期的4位 保持的4位 开始位置的4位
            byte[] leftBytes = new byte[] { 0x03, 0x01, 0x02, incrementBytes[3], incrementBytes[2], incrementBytes[1], incrementBytes[0], speedBytes[3], speedBytes[2], speedBytes[1], speedBytes[0], circleBytes[3], circleBytes[2], circleBytes[1], circleBytes[0], stayBytes[3], stayBytes[2], stayBytes[1], stayBytes[0], startBytes[3], startBytes[2], startBytes[1], startBytes[0], endNumberBytes[3], endNumberBytes[2], endNumberBytes[1], endNumberBytes[0] };
            byte[] requestFrame = GetByteArray(28, leftBytes);
            TcpMessageSend(requestFrame);
            int count = 0;
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            while (!isCoiledUntriggerReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isCoiledUntriggerReceived = false;
            isReadStatus = true;
        }

        bool isIntervalTriggerReceived = false;
        byte[] IntervalTriggerFeedback = new byte[] { 0x03, 0x01, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        /// <summary>
        /// 开始间隔扫描触发模式
        /// incrementNumber：终点-起点，startNumber：起点，stayNumber：保持时间，delayNumber：延迟时间，tinyInNumber：扫描增量
        /// </summary>
        public void Start_Interval_Trigger(int incrementNumber, int startNumber, int stayNumber, int delayNumber, int tinyInNumber, int direction)
        {
            isReadStatus = false;
            string incrementHexString = incrementNumber.ToString("X").PadLeft(8, '0');
            byte[] incrementBytes = Enumerable.Range(0, incrementHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(incrementHexString.Substring(x, 2), 16))
                                .ToArray();
            string startHexString = startNumber.ToString("X").PadLeft(8, '0');
            byte[] startBytes = Enumerable.Range(0, startHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(startHexString.Substring(x, 2), 16))
                                .ToArray();
            string stayHexString = stayNumber.ToString("X").PadLeft(8, '0');
            byte[] stayBytes = Enumerable.Range(0, stayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(stayHexString.Substring(x, 2), 16))
                                .ToArray();
            string delayHexString = delayNumber.ToString("X").PadLeft(8, '0');
            byte[] delayBytes = Enumerable.Range(0, delayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(delayHexString.Substring(x, 2), 16))
                                .ToArray();
            string tinyInHexString = tinyInNumber.ToString("X").PadLeft(8, '0');
            byte[] tinyInBytes = Enumerable.Range(0, tinyInHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(tinyInHexString.Substring(x, 2), 16))
                                .ToArray();
            string directionHexString = direction.ToString("X").PadLeft(8, '0');
            byte[] directionBytes = Enumerable.Range(0, directionHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(directionHexString.Substring(x, 2), 16))
                                .ToArray();
            // 03 01 05 4位总增量  4位扫描增量  4为延迟时间  4位保持时间  4为开始位置
            byte[] leftBytes = new byte[] { 0x03, 0x01, 0x05, incrementBytes[3], incrementBytes[2], incrementBytes[1], incrementBytes[0], tinyInBytes[3], tinyInBytes[2], tinyInBytes[1], tinyInBytes[0], delayBytes[3], delayBytes[2], delayBytes[1], delayBytes[0], stayBytes[3], stayBytes[2], stayBytes[1], stayBytes[0], startBytes[3], startBytes[2], startBytes[1], startBytes[0], directionBytes[3], directionBytes[2], directionBytes[1], directionBytes[0] };
            byte[] requestFrame = GetByteArray(28, leftBytes);
            TcpMessageSend(requestFrame);
            int count = 0;
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            //while (!isIntervalTriggerReceived && count < 5)
            //{
            //    TcpMessageSend(requestFrame);
            //    count++;
            //    Thread.Sleep(100);
            //}
            isIntervalTriggerReceived = false;
            isReadStatus = true;
        }

        bool isIntervalUntriggerReceived = false;
        byte[] IntervalUntriggerFeedback = new byte[] { 0x03, 0x01, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        /// <summary>
        /// 开始间隔扫描未触发模式
        /// incrementNumber：终点-起点，startNumber：起点，stayNumber：保持时间，delayNumber：延迟时间，tinyInNumber：扫描增量
        /// </summary>
        public void Start_Interval_Untrigger(int incrementNumber, int startNumber, int stayNumber, int delayNumber, int tinyInNumber, int direction)
        {
            isReadStatus = false;
            string incrementHexString = incrementNumber.ToString("X").PadLeft(8, '0');
            byte[] incrementBytes = Enumerable.Range(0, incrementHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(incrementHexString.Substring(x, 2), 16))
                                .ToArray();
            string startHexString = startNumber.ToString("X").PadLeft(8, '0');
            byte[] startBytes = Enumerable.Range(0, startHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(startHexString.Substring(x, 2), 16))
                                .ToArray();
            string stayHexString = stayNumber.ToString("X").PadLeft(8, '0');
            byte[] stayBytes = Enumerable.Range(0, stayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(stayHexString.Substring(x, 2), 16))
                                .ToArray();
            string delayHexString = delayNumber.ToString("X").PadLeft(8, '0');
            byte[] delayBytes = Enumerable.Range(0, delayHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(delayHexString.Substring(x, 2), 16))
                                .ToArray();
            string tinyInHexString = tinyInNumber.ToString("X").PadLeft(8, '0');
            byte[] tinyInBytes = Enumerable.Range(0, tinyInHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(tinyInHexString.Substring(x, 2), 16))
                                .ToArray();
            string directionHexString = direction.ToString("X").PadLeft(8, '0');
            byte[] directionBytes = Enumerable.Range(0, directionHexString.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(directionHexString.Substring(x, 2), 16))
                                .ToArray();
            // 03 01 04 4位总增量  4位扫描增量  4为延迟时间  4位保持时间  4为开始位置
            byte[] leftBytes = new byte[] { 0x03, 0x01, 0x04, incrementBytes[3], incrementBytes[2], incrementBytes[1], incrementBytes[0], tinyInBytes[3], tinyInBytes[2], tinyInBytes[1], tinyInBytes[0], delayBytes[3], delayBytes[2], delayBytes[1], delayBytes[0], stayBytes[3], stayBytes[2], stayBytes[1], stayBytes[0], startBytes[3], startBytes[2], startBytes[1], startBytes[0], directionBytes[3], directionBytes[2], directionBytes[1], directionBytes[0] };
            byte[] requestFrame = GetByteArray(28, leftBytes);
            TcpMessageSend(requestFrame);
            int count = 0;
            //serialPort.Write(requestFrame, 0, requestFrame.Length);
            while (!isIntervalUntriggerReceived && count < 5)
            {
                TcpMessageSend(requestFrame);
                count++;
                Thread.Sleep(100);
            }
            isIntervalUntriggerReceived = false;
            isReadStatus = true;
        }

        #endregion

    }
}
