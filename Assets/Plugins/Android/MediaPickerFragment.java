package com.barabashka;

import android.app.Activity;
import android.app.Fragment;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.List;

/**
 * Системное окно выбора файла (галерея, «Файлы») без лишних разрешений приложения.
 * Выбранное копируется в каталог приложения: картинки уменьшаются до 2560 пикселей по большей стороне и поворачиваются
 * по EXIF (Unity сам поворот не учитывает), для них же делается миниатюра 384 пикселя; остальные файлы копируются как есть.
 * Результат отправляется в Unity через UnitySendMessage: записи разделены переводом строки, поля табуляцией:
 * вид (0 картинка, 1 видео, 2 прочее), адрес file://, название, mime-тип, размер в байтах, адрес миниатюры (может быть пустым),
 * длительность видео в миллисекундах (0, если неизвестна).
 * Пустая строка: ничего не выбрано; строка "ERROR:текст": ошибка.
 */
public final class MediaPickerFragment extends Fragment {
    private static final String TAG = "BarabashkaMediaPicker";
    private static final int REQUEST = 7311;
    private static final int MAX_SIDE = 2560;
    private static final int THUMB_SIDE = 384;

    private String mime;
    private boolean multiple;
    private String receiver;
    private String method;
    private boolean started;

    /** Вызывается из C#: открывает окно выбора. mime: "image/*", "video/*" или "*&#47;*". */
    public static void pick(final Activity activity, final String mime, final boolean multiple,
                            final String receiver, final String method) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    MediaPickerFragment fragment = new MediaPickerFragment();
                    Bundle args = new Bundle();
                    args.putString("mime", mime);
                    args.putBoolean("multiple", multiple);
                    args.putString("receiver", receiver);
                    args.putString("method", method);
                    fragment.setArguments(args);
                    activity.getFragmentManager().beginTransaction().add(fragment, TAG).commitAllowingStateLoss();
                } catch (Throwable error) {
                    Log.w(TAG, "pick failed", error);
                    send(receiver, method, "ERROR:" + error.getMessage());
                }
            }
        });
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Bundle args = getArguments();
        if (args == null || savedInstanceState != null) {
            // Активность пересоздана системой: прежний запрос потерян.
            removeSelf();
            return;
        }
        mime = args.getString("mime", "image/*");
        multiple = args.getBoolean("multiple", false);
        receiver = args.getString("receiver", "");
        method = args.getString("method", "");
        try {
            Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
            intent.setType(mime);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, multiple);
            started = true;
            startActivityForResult(Intent.createChooser(intent, null), REQUEST);
        } catch (Throwable error) {
            Log.w(TAG, "start failed", error);
            send(receiver, method, "ERROR:" + error.getMessage());
            removeSelf();
        }
    }

    @Override
    public void onActivityResult(final int requestCode, final int resultCode, final Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST || !started) return;
        started = false;

        final List<Uri> uris = new ArrayList<Uri>();
        if (resultCode == Activity.RESULT_OK && data != null) {
            if (data.getClipData() != null) {
                for (int i = 0; i < data.getClipData().getItemCount(); i++) {
                    uris.add(data.getClipData().getItemAt(i).getUri());
                }
            } else if (data.getData() != null) {
                uris.add(data.getData());
            }
        }
        final Context context = getActivity() != null ? getActivity().getApplicationContext() : null;
        removeSelf();

        if (uris.isEmpty() || context == null) {
            send(receiver, method, "");
            return;
        }
        new Thread(new Runnable() {
            @Override
            public void run() {
                StringBuilder payload = new StringBuilder();
                for (Uri uri : uris) {
                    try {
                        String record = copyOne(context, uri);
                        if (record != null) {
                            if (payload.length() > 0) payload.append('\n');
                            payload.append(record);
                        }
                    } catch (Throwable error) {
                        Log.w(TAG, "copy failed", error);
                    }
                }
                send(receiver, method, payload.toString());
            }
        }, "BarabashkaMediaCopy").start();
    }

    private void removeSelf() {
        try {
            if (getFragmentManager() != null) {
                getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            }
        } catch (Throwable ignored) {
        }
    }

    private static void send(String receiver, String method, String payload) {
        try {
            UnityPlayer.UnitySendMessage(receiver, method, payload == null ? "" : payload);
        } catch (Throwable error) {
            Log.w(TAG, "send failed", error);
        }
    }

    // ---- копирование ----

    private static String copyOne(Context context, Uri uri) throws Exception {
        ContentResolver resolver = context.getContentResolver();
        String type = resolver.getType(uri);
        if (type == null) type = "";
        String name = displayName(resolver, uri);

        File dir = new File(context.getFilesDir(), "media");
        if (!dir.exists() && !dir.mkdirs()) return null;
        String stamp = Long.toString(System.currentTimeMillis()) + "_" + Integer.toString((int) (Math.random() * 1000));

        if (type.startsWith("image/")) {
            boolean png = type.equals("image/png");
            File out = new File(dir, stamp + (png ? ".png" : ".jpg"));
            File thumb = new File(dir, stamp + "_t.jpg");
            if (!saveImage(resolver, uri, out, thumb, png)) {
                copyStream(resolver, uri, out);
                thumb = null;
            }
            return record(0, out, name, png ? "image/png" : "image/jpeg", thumb, 0);
        }

        String extension = extensionOf(name, type);
        File out = new File(dir, stamp + extension);
        copyStream(resolver, uri, out);
        if (type.startsWith("video/")) {
            File thumb = new File(dir, stamp + "_t.jpg");
            long duration = videoInfo(out, thumb);
            return record(1, out, name, type, thumb, duration);
        }
        return record(2, out, name, type, null, 0);
    }

    private static String record(int kind, File file, String name, String type, File thumb, long durationMs) {
        String title = name == null ? file.getName() : name.replace('\t', ' ').replace('\n', ' ').replace('\r', ' ');
        return kind + "\t" + "file://" + file.getAbsolutePath() + "\t" + title + "\t" + type + "\t" + file.length() + "\t"
                + (thumb != null && thumb.exists() ? "file://" + thumb.getAbsolutePath() : "") + "\t" + durationMs;
    }

    /** Кадр из видео для миниатюры и длительность в миллисекундах; при любой ошибке миниатюры не будет, длительность 0. */
    private static long videoInfo(File video, File thumb) {
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        long duration = 0;
        try {
            retriever.setDataSource(video.getAbsolutePath());
            String value = retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION);
            if (value != null) duration = Long.parseLong(value);

            Bitmap frame = retriever.getFrameAtTime(Math.min(1000000L, duration * 1000L / 4), MediaMetadataRetriever.OPTION_CLOSEST_SYNC);
            if (frame == null) frame = retriever.getFrameAtTime();
            if (frame != null) {
                Bitmap small = frame;
                try {
                    int w = frame.getWidth(), h = frame.getHeight();
                    float k = (float) THUMB_SIDE / Math.max(w, h);
                    if (k < 1f) small = Bitmap.createScaledBitmap(frame, Math.max(1, Math.round(w * k)), Math.max(1, Math.round(h * k)), true);
                    writeBitmap(small, thumb, false);
                } finally {
                    if (small != frame) small.recycle();
                    frame.recycle();
                }
            }
        } catch (Throwable error) {
            Log.w(TAG, "video info failed", error);
        } finally {
            try { retriever.release(); } catch (Throwable ignored) { }
        }
        return duration;
    }

    private static String displayName(ContentResolver resolver, Uri uri) {
        Cursor cursor = null;
        try {
            cursor = resolver.query(uri, new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null);
            if (cursor != null && cursor.moveToFirst()) {
                int column = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (column >= 0) return cursor.getString(column);
            }
        } catch (Throwable ignored) {
        } finally {
            if (cursor != null) cursor.close();
        }
        String last = uri.getLastPathSegment();
        return last == null ? "file" : last;
    }

    private static String extensionOf(String name, String type) {
        if (name != null) {
            int dot = name.lastIndexOf('.');
            if (dot >= 0 && name.length() - dot <= 6) {
                String ext = name.substring(dot).toLowerCase();
                if (ext.matches("\\.[a-z0-9]+")) return ext;
            }
        }
        if (type.equals("video/mp4")) return ".mp4";
        if (type.equals("video/webm")) return ".webm";
        return ".bin";
    }

    private static void copyStream(ContentResolver resolver, Uri uri, File out) throws Exception {
        InputStream in = null;
        OutputStream os = null;
        try {
            in = resolver.openInputStream(uri);
            if (in == null) throw new IllegalStateException("no stream");
            os = new FileOutputStream(out);
            byte[] buffer = new byte[64 * 1024];
            int n;
            while ((n = in.read(buffer)) > 0) os.write(buffer, 0, n);
        } finally {
            if (in != null) try { in.close(); } catch (Throwable ignored) { }
            if (os != null) try { os.close(); } catch (Throwable ignored) { }
        }
    }

    /** Уменьшает, поворачивает по EXIF и сохраняет картинку и миниатюру. false: не получилось (например, нехватка памяти). */
    private static boolean saveImage(ContentResolver resolver, Uri uri, File out, File thumb, boolean png) {
        Bitmap bitmap = null;
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream in = resolver.openInputStream(uri);
            if (in == null) return false;
            try { BitmapFactory.decodeStream(in, null, bounds); } finally { in.close(); }
            if (bounds.outWidth <= 0 || bounds.outHeight <= 0) return false;

            int sample = 1;
            int longSide = Math.max(bounds.outWidth, bounds.outHeight);
            while (longSide / (sample * 2) >= MAX_SIDE) sample *= 2;

            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inSampleSize = sample;
            in = resolver.openInputStream(uri);
            if (in == null) return false;
            try { bitmap = BitmapFactory.decodeStream(in, null, options); } finally { in.close(); }
            if (bitmap == null) return false;

            int orientation = ExifInterface.ORIENTATION_NORMAL;
            try {
                in = resolver.openInputStream(uri);
                if (in != null) {
                    try {
                        orientation = new ExifInterface(in).getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL);
                    } finally {
                        in.close();
                    }
                }
            } catch (Throwable ignored) {
            }

            Matrix matrix = new Matrix();
            switch (orientation) {
                case ExifInterface.ORIENTATION_ROTATE_90: matrix.postRotate(90); break;
                case ExifInterface.ORIENTATION_ROTATE_180: matrix.postRotate(180); break;
                case ExifInterface.ORIENTATION_ROTATE_270: matrix.postRotate(270); break;
                case ExifInterface.ORIENTATION_FLIP_HORIZONTAL: matrix.postScale(-1, 1); break;
                case ExifInterface.ORIENTATION_FLIP_VERTICAL: matrix.postScale(1, -1); break;
                case ExifInterface.ORIENTATION_TRANSPOSE: matrix.postRotate(90); matrix.postScale(-1, 1); break;
                case ExifInterface.ORIENTATION_TRANSVERSE: matrix.postRotate(270); matrix.postScale(-1, 1); break;
                default: break;
            }
            int side = Math.max(bitmap.getWidth(), bitmap.getHeight());
            if (side > MAX_SIDE) {
                float k = (float) MAX_SIDE / side;
                matrix.postScale(k, k);
            }
            if (!matrix.isIdentity()) {
                Bitmap changed = Bitmap.createBitmap(bitmap, 0, 0, bitmap.getWidth(), bitmap.getHeight(), matrix, true);
                if (changed != bitmap) { bitmap.recycle(); bitmap = changed; }
            }

            writeBitmap(bitmap, out, png);

            int tw = bitmap.getWidth(), th = bitmap.getHeight();
            float tk = (float) THUMB_SIDE / Math.max(tw, th);
            Bitmap small = tk < 1f ? Bitmap.createScaledBitmap(bitmap, Math.max(1, Math.round(tw * tk)), Math.max(1, Math.round(th * tk)), true) : bitmap;
            try {
                writeBitmap(small, thumb, false);
            } finally {
                if (small != bitmap) small.recycle();
            }
            return true;
        } catch (OutOfMemoryError error) {
            Log.w(TAG, "image too large", error);
            return false;
        } catch (Throwable error) {
            Log.w(TAG, "image save failed", error);
            return false;
        } finally {
            if (bitmap != null) bitmap.recycle();
        }
    }

    private static void writeBitmap(Bitmap bitmap, File file, boolean png) throws Exception {
        OutputStream os = new FileOutputStream(file);
        try {
            bitmap.compress(png ? Bitmap.CompressFormat.PNG : Bitmap.CompressFormat.JPEG, png ? 100 : 90, os);
        } finally {
            os.close();
        }
    }
}
