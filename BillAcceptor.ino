/*
   Bill Acceptor Controller
   This code is specifically for controlling a bill acceptor device
   
   Made by Mateo Velez - Metavix
   Modified and separated by Cascade
*/

#include <avr/pgmspace.h>
#include <SoftwareSerial.h>

// Debug configuration
//#define DEBUG_METAVIX
bool debugFlag = false;

// Communication settings
#define BAUDRATE_PC 57600
#define BAUDRATE_ACCEPTANCE 9600

// Constants
#define RESPONSE_SIZE 254
#define MESSAGE_SIZE 254
#define COMMANDS_SIZE 50
#define TIMEOUT 5000

// States definitions
#define STATE_ACCEPTANCE_STATUS 0
#define STATE_ACCEPTANCE_STACK 1
#define STATE_ACCEPTANCE_SEND_ACK 2
#define STATE_ACCEPTANCE_GET_DATA 3
#define STATE_ACCEPTANCE_INITIALIZE 4
#define STATE_ACCEPTANCE_RESET 5
#define STATE_ACCEPTANCE_FATAL_ERROR 6
#define STATE_ACCEPTANCE_VEND_VALID 7
#define STATE_WAITTING_FOR_COMMAND 8

// Global variables
char commands[COMMANDS_SIZE];
char response[RESPONSE_SIZE];
char message[MESSAGE_SIZE];
String messageSplited[10];
String orders[4];
uint8_t state = STATE_WAITTING_FOR_COMMAND;
uint8_t lastState = STATE_WAITTING_FOR_COMMAND;
bool acceptFlag = false;
bool flagBills = 0;
uint32_t unMoney = 0;

// Status response table and flags
uint8_t statusResponseTable[70];
uint8_t flagForResponseAction[70];
String errorMessage[12];
bool flagForPowerUp = false;

void setup() {
  Serial.begin(BAUDRATE_PC);
  Serial1.begin(BAUDRATE_ACCEPTANCE);
  
  setPredefinedConfiguration();
  statusCheckerFilling();
  errorMessageFilling();
  
  state = STATE_ACCEPTANCE_INITIALIZE;
}

void loop() {
  switch(state) {
    case STATE_ACCEPTANCE_STATUS:
      stateAcceptanceStatus();
      break;
    case STATE_ACCEPTANCE_STACK:
      stateAcceptanceStack();
      break;
    case STATE_ACCEPTANCE_SEND_ACK:
      stateAcceptanceSendAck();
      break;
    case STATE_ACCEPTANCE_GET_DATA:
      stateAcceptanceGetData();
      break;
    case STATE_ACCEPTANCE_INITIALIZE:
      stateAcceptanceInitialize();
      break;
    case STATE_ACCEPTANCE_RESET:
      stateAcceptanceReset();
      break;
    case STATE_ACCEPTANCE_FATAL_ERROR:
      stateAcceptanceFatalError();
      break;
    case STATE_ACCEPTANCE_VEND_VALID:
      stateAcceptanceVendValid();
      break;
    case STATE_WAITTING_FOR_COMMAND:
      stateWaittingForCommand();
      break;
    default:
      state = STATE_WAITTING_FOR_COMMAND;
      break;
  }
}

// Helper Functions
void sendCmd(char* cmd, int len) {
  Serial1.write(cmd, len);
}

void clearMessage() {
  memset(message, '\0', MESSAGE_SIZE);
}

void clearMessageSplited() {
  for (int i = 0; i < 10; i++) {
    messageSplited[i] = "";
  }
}

void debugMetavixln(String message) {
  #ifdef DEBUG_METAVIX
  if (debugFlag) Serial.println(message);
  #endif
}

void debugMetavix(String message) {
  #ifdef DEBUG_METAVIX
  if (debugFlag) Serial.print(message);
  #endif
}

// State Functions
void stateAcceptanceSendAck() {
  debugMetavixln(F("Inside state send ACK"));
  commandAck();
  sendCmd(commands, (int)commands[1]);
  delay(100);
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceFatalError() {
  int i = 0;
  for (i = 52; i < 62; ) {
    if (response[3] == statusResponseTable[i]) {
      Serial.print(F("ER:AP:FATAL:"));
      Serial.println(errorMessage[i - 29]);
    }
  }
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceReset() {
  debugMetavixln(F("Inside Reset"));
  commandReset();
  sendCmd(commands, (int)commands[1]);
  delay(100);
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceStatus() {
  debugMetavixln(F("Inside status"));
  int k = 0;
  commandStatus();
  sendCmd(commands, (int)commands[1]);
  while(!Serial1.available());
  readResponse();
  for (k = 0; k < 22; k++) {
    if (response[2] == statusResponseTable[k]) {
      break;
    }
  }
  if (k > 10) {
    Serial.print(F("ER:AP:"));
    Serial.println(errorMessage[k - 11]);
  }
  debugMetavixln("The state is: " + String(k));
  state = flagForResponseAction[k];
  if (flagForPowerUp == true && response[2] == DISABLE) {
    state = STATE_ACCEPTANCE_INITIALIZE;
    flagForPowerUp = true;
  }
}

void stateAcceptanceStack() {
  debugMetavixln(F("Inside stack"));
  volatile int i = 0;
  for (i = 62; i < 69; i++) {
    if (response[3] == statusResponseTable[i]) {
      unMoney = values[i - 62];
      delay(5);
    }
  }
  commandStack1();
  sendCmd(commands, (int)commands[1]);
  delay(100);
  readResponse();
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceGetData() {
  debugMetavixln(F("Inside get data"));
  volatile int i = 0;
  for (i = 41; i < 52; i++) {
    if (response[3] == statusResponseTable[i]) {
      Serial.print(F("ER:AP:"));
      Serial.println(errorMessage[i - 29]);
    }
  }
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceInitialize() {
  debugMetavixln(F("Inside initialize"));
  commandEnable();
  sendCmd(commands, (int)commands[1]);
  delay(150);
  readResponse();
  if (!getResponse(commands[2], response[2]))
    return;
  commandSecurity();
  sendCmd(commands, (int)commands[1]);
  delay(100);
  readResponse();
  if (!getResponse(commands[2], response[2]))
    return;
  state = STATE_ACCEPTANCE_STATUS;
}

void stateAcceptanceVendValid() {
  debugMetavixln(F("Inside state acceptance vend valid"));
  Serial.print("UN:AP:");
  Serial.println(String(unMoney));
  unMoney = 0;
  state = STATE_ACCEPTANCE_SEND_ACK;
}

void stateWaittingForCommand() {
  debugMetavixln(F("Inside state waiting for command"));
  clearMessageSplited();
  int i = 0;
  lastState = state;
  if (message[0] == '\0') {
    usbReceiver();
    delay(5);
    return;
  }
  char *p = message;
  char *str;

  while ((str = strtok_r(p, ":", &p)) != NULL) {
    messageSplited[i] = String(str);
    i++;
  }

  if (messageSplited[1] == orders[0]) {
    flagBills = 1;
    setInhibit(false);
    i = 0;
  } else {
    for (i = 1; i < 3; i++) {
      if (messageSplited[2] == orders[i])
        break;
    }
  }
  clearMessage();
  state = ordersState[i];
}

// Configuration Functions
void statusCheckerFilling() {
  // Fill status response table and flags
  // Add your status response table initialization here
}

void errorMessageFilling() {
  // Fill error messages
  errorMessage[0] = F("STACKER_FULL");
  errorMessage[1] = F("STACKER_OPEN");
  errorMessage[2] = F("JAM_IN_ACCEPTOR");
  errorMessage[3] = F("JAM_IN_STACKER");
  errorMessage[4] = F("PAUSE");
  errorMessage[5] = F("CHEATED");
  errorMessage[6] = F("FAILURE");
  errorMessage[7] = F("COMMUNICATION_ERROR");
  // Add more error messages as needed
}

void setPredefinedConfiguration() {
  // Add your predefined configuration initialization here
}

// Command Functions
void commandStatus() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x11;
  commands[3] = 0x12;
}

void commandStack1() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x41;
  commands[3] = 0x42;
}

void commandAck() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x50;
  commands[3] = 0x51;
}

void commandReset() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x40;
  commands[3] = 0x41;
}

void commandEnable() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x3E;
  commands[3] = 0x3F;
}

void commandSecurity() {
  commands[0] = 0x02;
  commands[1] = 0x03;
  commands[2] = 0x3F;
  commands[3] = 0x40;
}

// Utility Functions
void readResponse() {
  int i = 0;
  while (Serial1.available()) {
    response[i] = Serial1.read();
    i++;
    delay(1);
  }
}

bool getResponse(uint8_t command, uint8_t responseCommand) {
  if (command == responseCommand)
    return true;
  return false;
}

void usbReceiver() {
  int i = 0;
  while (Serial.available()) {
    message[i] = Serial.read();
    i++;
    delay(1);
  }
}

void setInhibit(bool enable) {
  // Add your inhibit control code here
}
