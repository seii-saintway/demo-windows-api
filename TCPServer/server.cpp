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

    SOCKET sockListen = socket(AF_INET, SOCK_STREAM, 0);
    SOCKADDR_IN addrServer = {AF_INET, htons(6000)};

    bind(sockListen, (SOCKADDR *) &addrServer, sizeof(SOCKADDR));
    listen(sockListen, 5);

    while(true) {
        SOCKADDR_IN addrClient;
        int addrlenClient = sizeof(SOCKADDR);

        SOCKET sockAccept = accept(sockListen, (SOCKADDR *) &addrClient, &addrlenClient);
        if(sockAccept == INVALID_SOCKET) continue;

        char sendBuf[100];
        char recvBuf[100];

        printf("\n");

        send(sockAccept, sendBuf, sprintf(sendBuf, "Welcome %s:%d!", inet_ntoa(addrClient.sin_addr), ntohs(addrClient.sin_port))+1, 0);
        printf(">>> %s\n", sendBuf);

        recv(sockAccept, recvBuf, 100, 0);
        printf("<<< %s\n", recvBuf);

        do {
            printf("\n");

            printf(">>> ");    gets(sendBuf);
            send(sockAccept, sendBuf, strlen(sendBuf)+1, 0);

            recv(sockAccept, recvBuf, 100, 0);
            printf("<<< %s\n", recvBuf);
        } while(strcmp(recvBuf, "exit") != 0);

        closesocket(sockAccept);
    }

    closesocket(sockListen);

    WSACleanup();
    return 0;
}
