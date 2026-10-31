package com.barabashka;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;

/**
 * Отдаёт другим приложениям (просмотрщикам PDF, Word и т.п.) документы из папки media-inbox/documents на чтение.
 * Адрес: content://com.barabashka.docs/doc?path=абсолютный_путь. Путь проверяется: только файлы внутри папки документов
 * приложения, ничего больше отдать нельзя. Нужен потому, что с Android 7 файл нельзя передать чужому приложению по адресу file://.
 */
public final class BarabashkaDocsProvider extends ContentProvider {
    private static final String SEGMENT = File.separator + "media-inbox" + File.separator + "documents" + File.separator;

    @Override
    public boolean onCreate() {
        return true;
    }

    /** Файл по адресу или null, если путь не разрешён. */
    private File resolve(Uri uri) {
        try {
            String path = uri.getQueryParameter("path");
            if (path == null || getContext() == null) return null;
            File file = new File(path).getCanonicalFile();
            String canonical = file.getPath();
            if (!file.isFile() || !canonical.contains(SEGMENT)) return null;

            File external = getContext().getExternalFilesDir(null);
            File internal = getContext().getFilesDir();
            boolean inside = false;
            if (external != null && canonical.startsWith(external.getCanonicalPath() + File.separator)) inside = true;
            if (internal != null && canonical.startsWith(internal.getCanonicalPath() + File.separator)) inside = true;
            return inside ? file : null;
        } catch (Throwable error) {
            return null;
        }
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        File file = resolve(uri);
        if (file == null) throw new FileNotFoundException("not allowed");
        return ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        File file = resolve(uri);
        if (file == null) return null;
        MatrixCursor cursor = new MatrixCursor(new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE});
        cursor.addRow(new Object[]{file.getName(), Long.valueOf(file.length())});
        return cursor;
    }

    @Override
    public String getType(Uri uri) {
        File file = resolve(uri);
        if (file == null) return null;
        String name = file.getName().toLowerCase();
        if (name.endsWith(".pdf")) return "application/pdf";
        if (name.endsWith(".docx")) return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (name.endsWith(".doc")) return "application/msword";
        if (name.endsWith(".xlsx")) return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        if (name.endsWith(".xls")) return "application/vnd.ms-excel";
        if (name.endsWith(".txt")) return "text/plain";
        return "application/octet-stream";
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;
    }
}
