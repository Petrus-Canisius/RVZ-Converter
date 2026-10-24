/* WIT SHA-1 adapter. Copyright (c) 2026 Petrus-Canisius.
 * SPDX-License-Identifier: GPL-2.0-or-later
 * Hash implementation: Mbed TLS contributors, Apache-2.0. */
#include "crypto/rvz-sha1.h"
#include <stdio.h>
#include <stdlib.h>

static void require_success(int result)
{
    if (result != 0) {
        fprintf(stderr, "WIT SHA-1 operation failed (Mbed TLS error %d).\n", result);
        abort();
    }
}
int RVZ_SHA1_Init(SHA_CTX *context)
{
    mbedtls_sha1_init(context);
    require_success(mbedtls_sha1_starts_ret(context));
    return 1;
}
int RVZ_SHA1_Update(SHA_CTX *context, const void *data, size_t length)
{
    require_success(mbedtls_sha1_update_ret(context, data, length));
    return 1;
}
int RVZ_SHA1_Final(unsigned char *digest, SHA_CTX *context)
{
    require_success(mbedtls_sha1_finish_ret(context, digest));
    mbedtls_sha1_free(context);
    return 1;
}
unsigned char *RVZ_SHA1(const unsigned char *data, size_t length, unsigned char *digest)
{
    static unsigned char fallback[SHA_DIGEST_LENGTH];
    if (!digest) digest = fallback;
    require_success(mbedtls_sha1_ret(data, length, digest));
    return digest;
}
