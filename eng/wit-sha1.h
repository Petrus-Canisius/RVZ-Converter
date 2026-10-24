/* WIT SHA-1 adapter. Copyright (c) 2026 Petrus-Canisius.
 * SPDX-License-Identifier: GPL-2.0-or-later
 * Hash implementation: Mbed TLS contributors, Apache-2.0. */
#ifndef RVZ_WIT_SHA1_H
#define RVZ_WIT_SHA1_H
#include <stddef.h>
#include "mbedtls/sha1.h"
#define SHA_DIGEST_LENGTH 20
typedef mbedtls_sha1_context SHA_CTX;
int RVZ_SHA1_Init(SHA_CTX *context);
int RVZ_SHA1_Update(SHA_CTX *context, const void *data, size_t length);
int RVZ_SHA1_Final(unsigned char *digest, SHA_CTX *context);
unsigned char *RVZ_SHA1(const unsigned char *data, size_t length, unsigned char *digest);
#endif
