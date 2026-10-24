/* Native known-answer checks. Copyright (c) 2026 Petrus-Canisius.
 * SPDX-License-Identifier: GPL-2.0-or-later */
#include "crypto/rvz-sha1.h"
#include <stdio.h>
#include <string.h>

static int check(const unsigned char *digest, const char *expected)
{
    char text[41];
    for (int i = 0; i < 20; i++) sprintf(text + i * 2, "%02x", digest[i]);
    if (strcmp(text, expected)) { fprintf(stderr, "SHA-1 mismatch: %s\n", text); return 1; }
    return 0;
}
int main(void)
{
    unsigned char digest[20], chunk[1000]; SHA_CTX context;
    RVZ_SHA1((const unsigned char *)"", 0, digest);
    if (check(digest, "da39a3ee5e6b4b0d3255bfef95601890afd80709")) return 1;
    RVZ_SHA1((const unsigned char *)"abc", 3, digest);
    if (check(digest, "a9993e364706816aba3e25717850c26c9cd0d89d")) return 1;
    RVZ_SHA1_Init(&context);
    for (int i = 0; i < 3; i++) RVZ_SHA1_Update(&context, "abc" + i, 1);
    RVZ_SHA1_Final(digest, &context);
    if (check(digest, "a9993e364706816aba3e25717850c26c9cd0d89d")) return 1;
    memset(chunk, 'a', sizeof(chunk)); RVZ_SHA1_Init(&context);
    for (int i = 0; i < 1000; i++) RVZ_SHA1_Update(&context, chunk, sizeof(chunk));
    RVZ_SHA1_Final(digest, &context);
    if (check(digest, "34aa973cd4c4daa4f61eeb2bdbad27316534016f")) return 1;
    puts("PASS Mbed TLS SHA-1: empty, abc, incremental abc and one million a bytes.");
    return 0;
}
