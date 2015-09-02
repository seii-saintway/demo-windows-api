/*--------------------------------------------------------
  HelloMsg - Displays "Hello, Windows!" in a message box
--------------------------------------------------------*/

#include <windows.h>

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR szCmdLine, int iCmdShow) {
	MessageBox(NULL, TEXT("Hello, Windows!"), TEXT("HelloMsg"), 0);
	return 0;
}
