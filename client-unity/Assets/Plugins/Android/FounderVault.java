package site.arnavigator.spell;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.SecureRandom;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Encrypt installation identity and optional founder grant at rest using a non-exportable app key. */
public final class FounderVault {
    private static final String ALIAS = "spell-founder-v1";
    private static final String ANDROID_KEYSTORE = "AndroidKeyStore";

    private static SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance(ANDROID_KEYSTORE);
        store.load(null);
        if (store.containsAlias(ALIAS)) return (SecretKey)store.getKey(ALIAS, null);
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
        return generator.generateKey();
    }
    private static SharedPreferences prefs(Context c) { return c.getSharedPreferences(ALIAS, Context.MODE_PRIVATE); }
    private static String read(Context c, String field) throws Exception {
        String data = prefs(c).getString(field, "");
        if (data.isEmpty()) return "";
        byte[] blob = Base64.decode(data, Base64.NO_WRAP);
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, blob, 0, 12));
        cipher.updateAAD(field.getBytes(StandardCharsets.UTF_8));
        return new String(cipher.doFinal(blob, 12, blob.length - 12), StandardCharsets.UTF_8);
    }
    private static void write(Context c, String field, String text) throws Exception {
        SharedPreferences.Editor editor = prefs(c).edit();
        if (text.isEmpty()) editor.remove(field);
        else {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key());
            cipher.updateAAD(field.getBytes(StandardCharsets.UTF_8));
            byte[] encrypted = cipher.doFinal(text.getBytes(StandardCharsets.UTF_8));
            byte[] blob = new byte[12 + encrypted.length];
            System.arraycopy(cipher.getIV(), 0, blob, 0, 12);
            System.arraycopy(encrypted, 0, blob, 12, encrypted.length);
            editor.putString(field, Base64.encodeToString(blob, Base64.NO_WRAP));
        }
        if (!editor.commit()) throw new java.io.IOException("Founder vault write failed.");
    }
    public static synchronized String identity(Context c) throws Exception {
        String secret = read(c, "device");
        if (!secret.isEmpty()) return secret;
        byte[] bytes = new byte[32];
        new SecureRandom().nextBytes(bytes);
        StringBuilder result = new StringBuilder(64);
        for (byte b : bytes) result.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        write(c, "device", result.toString());
        return result.toString();
    }
    public static synchronized String grant(Context c) throws Exception { return read(c, "grant"); }
    public static synchronized void remember(Context c, String token) throws Exception { write(c, "grant", token); }
}
