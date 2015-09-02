#include <Winsock2.h>
#include <stdio.h>

#pragma comment(lib, "ws2_32.lib")

int main() {
    WORD wVersionRequested = MAKEWORD(1, 1);
    WSADATA wsaData;

    int err = WSAStartup(wVersionRequested, &wsaData);
    if(err) return err;

    if(LOBYTE(wsaData.wVersion)!=1 || HIBYTE(wsaData.wVersion)!=1) {
        WSACleanup();
        return 0;
    }

    SOCKET sockClient = socket(AF_INET, SOCK_STREAM, 0);
    SOCKADDR_IN addrServer = {AF_INET, htons(6000), {127, 0, 0, 1}};

    if(connect(sockClient, (SOCKADDR *) &addrServer, sizeof(SOCKADDR))) {
        WSACleanup();
        return 0;
    }

    char recvBuf[100];
    char sendBuf[100];

    printf("\n");

    recv(sockClient, recvBuf, 100, 0);
    printf("<<< %s\n", recvBuf);

    const char name[] = "Andrew";
    send(sockClient, sendBuf, sprintf(sendBuf, "This is %s.", name)+1, 0);
    printf(">>> %s\n", sendBuf);

    do {
        printf("\n");

        recv(sockClient, recvBuf, 100, 0);
        printf("<<< %s\n", recvBuf);

        printf(">>> ");    gets(sendBuf);
        send(sockClient, sendBuf, strlen(sendBuf)+1, 0);
    } while(strcmp(sendBuf, "exit") != 0);

    closesocket(sockClient);

    WSACleanup();
    return 0;
}
