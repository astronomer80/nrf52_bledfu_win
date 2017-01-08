# nrf52_bledfu_win
Console Windows app to upload an application via BLE on nRF52 MCU that has the DFU Service installed.

DFU Procedure performed by this application:

1)Send 'START DFU' opcode + Application Command (0x0104)

2)Send the image size

3)Send 'INIT DFU' Command (0x0200): Called in the controlPoint_CalueChanged event invoked when the BLE device replies after sending the image size.

4)Transmit the Init image (The file DAT content)

5)Send 'INIT DFU' + Complete Command (0x0201)

6)Send packet receipt notification interval (currently 10)

7)Send 'RECEIVE FIRMWARE IMAGE' command to set DFU in firmware receive state.  (0x0300)

8)Send bin array contents as a series of packets (burst mode).  Each segment is pkt_payload_size bytes long. For every packet send, wait for notification.

9)Send Validate Command (0x0400)

10)Send Activate and Reset Command (0x0500)



