import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { Image } from 'expo-image';
import * as ImagePicker from 'expo-image-picker';
import * as ImageManipulator from 'expo-image-manipulator';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { AppText } from '@/components/ui/AppText';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { useToast } from '@/components/ui/Toast';
import { useDeleteReceipt, useUploadReceipt } from '@/features/receipts/hooks';
import { useAuthenticatedImageSource } from '@/features/receipts/use-authenticated-image';
import { useAppTheme } from '@/theme/ThemeProvider';
import type { ReceiptSummary } from '@/types/api';

const MAX_RECEIPTS = 5;

/** Longest edge after downscaling. Plenty to read a receipt; a fraction of the bytes. */
const MAX_DIMENSION = 1600;

export type ReceiptPickerProps = {
  transactionId: string;
  receipts: ReceiptSummary[];
};

/**
 * Attaches receipt photos to a transaction.
 *
 * Images are downscaled and re-encoded before upload. A modern phone camera
 * produces 4-12 MB HEIC files; sending those raw would blow the server's 10 MB
 * limit, cost the user real money on mobile data, and take long enough that
 * they assume the app has hung. Re-encoding to JPEG also normalises HEIC, which
 * not every downstream viewer can open.
 */
export function ReceiptPicker({ transactionId, receipts }: ReceiptPickerProps) {
  const theme = useAppTheme();
  const toast = useToast();

  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState(0);
  const [pendingDelete, setPendingDelete] = useState<ReceiptSummary | null>(null);

  const upload = useUploadReceipt();
  const remove = useDeleteReceipt();

  const atLimit = receipts.length >= MAX_RECEIPTS;

  const pick = async (source: 'camera' | 'library') => {
    if (atLimit) {
      toast.show({
        title: 'Receipt limit reached',
        description: `You can attach up to ${MAX_RECEIPTS} receipts.`,
        tone: 'warning',
      });
      return;
    }

    // Permission is requested at the moment of use, not at launch — asking for
    // the camera before the user has expressed any intent to use it is how apps
    // get denied permanently.
    const permission =
      source === 'camera'
        ? await ImagePicker.requestCameraPermissionsAsync()
        : await ImagePicker.requestMediaLibraryPermissionsAsync();

    if (!permission.granted) {
      Alert.alert(
        source === 'camera' ? 'Camera access needed' : 'Photo access needed',
        'Enable access in Settings to attach a receipt.',
      );
      return;
    }

    const result =
      source === 'camera'
        ? await ImagePicker.launchCameraAsync({
            mediaTypes: ['images'],
            quality: 0.9,
            exif: false,
          })
        : await ImagePicker.launchImageLibraryAsync({
            mediaTypes: ['images'],
            quality: 0.9,
            exif: false,
          });

    if (result.canceled || !result.assets[0]) return;

    const asset = result.assets[0];

    setBusy(true);
    setProgress(0);

    try {
      const longestEdge = Math.max(asset.width ?? 0, asset.height ?? 0);
      const needsResize = longestEdge > MAX_DIMENSION;

      const context = ImageManipulator.ImageManipulator.manipulate(asset.uri);

      if (needsResize) {
        const isLandscape = (asset.width ?? 0) >= (asset.height ?? 0);
        context.resize(
          isLandscape ? { width: MAX_DIMENSION } : { height: MAX_DIMENSION },
        );
      }

      const rendered = await context.renderAsync();
      const output = await rendered.saveAsync({
        compress: 0.75,
        format: ImageManipulator.SaveFormat.JPEG,
      });

      await upload.mutateAsync({
        transactionId,
        uri: output.uri,
        // The server generates its own storage name; this is only a display
        // label, so a generated one is safer than echoing the device's path.
        fileName: `receipt-${Date.now()}.jpg`,
        mimeType: 'image/jpeg',
        onProgress: setProgress,
      });

      toast.show({ title: 'Receipt attached', tone: 'success' });
    } catch (error) {
      toast.show({
        title: 'Could not attach receipt',
        description: error instanceof Error ? error.message : undefined,
        tone: 'error',
      });
    } finally {
      setBusy(false);
      setProgress(0);
    }
  };

  return (
    <View>
      <AppText variant="bodySmallStrong" color="textSecondary" style={{ marginBottom: 8 }}>
        Receipts
        <AppText variant="caption" color="textTertiary">
          {`  ${receipts.length}/${MAX_RECEIPTS}`}
        </AppText>
      </AppText>

      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ gap: theme.spacing.sm }}
      >
        {receipts.map((receipt) => (
          <ReceiptThumbnail
            key={receipt.id}
            receipt={receipt}
            onDelete={() => setPendingDelete(receipt)}
          />
        ))}

        {!atLimit ? (
          <>
            <AddTile
              icon="camera-outline"
              label="Camera"
              busy={busy}
              progress={progress}
              onPress={() => void pick('camera')}
            />
            <AddTile
              icon="image-outline"
              label="Gallery"
              busy={busy}
              progress={progress}
              onPress={() => void pick('library')}
            />
          </>
        ) : null}
      </ScrollView>

      <ConfirmDialog
        visible={pendingDelete !== null}
        title="Remove receipt?"
        message="This image will be permanently deleted."
        confirmLabel="Remove"
        destructive
        loading={remove.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return;

          try {
            await remove.mutateAsync({ id: pendingDelete.id, transactionId });
            toast.show({ title: 'Receipt removed', tone: 'success' });
          } catch (error) {
            toast.show({
              title: 'Could not remove receipt',
              description: error instanceof Error ? error.message : undefined,
              tone: 'error',
            });
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </View>
  );
}

function ReceiptThumbnail({
  receipt,
  onDelete,
}: {
  receipt: ReceiptSummary;
  onDelete: () => void;
}) {
  const theme = useAppTheme();
  const source = useAuthenticatedImageSource(receipt.url);

  return (
    <View>
      <Image
        source={source}
        style={[styles.tile, { borderRadius: theme.radius.medium, backgroundColor: theme.c.surfaceSunken }]}
        contentFit="cover"
        transition={150}
        accessibilityLabel={`Receipt ${receipt.fileName}`}
      />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Remove receipt ${receipt.fileName}`}
        onPress={onDelete}
        hitSlop={8}
        style={[
          styles.deleteBadge,
          { backgroundColor: theme.c.error, borderColor: theme.c.surface },
        ]}
      >
        <MaterialCommunityIcons name="close" size={12} color={theme.c.onPrimary} />
      </Pressable>
    </View>
  );
}

function AddTile({
  icon,
  label,
  busy,
  progress,
  onPress,
}: {
  icon: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  label: string;
  busy: boolean;
  progress: number;
  onPress: () => void;
}) {
  const theme = useAppTheme();

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Add receipt from ${label.toLowerCase()}`}
      accessibilityState={{ busy, disabled: busy }}
      disabled={busy}
      onPress={onPress}
      style={[
        styles.tile,
        styles.addTile,
        {
          borderRadius: theme.radius.medium,
          borderColor: theme.c.border,
          backgroundColor: theme.c.surfaceSunken,
          opacity: busy ? theme.opacity.disabled : 1,
        },
      ]}
    >
      {busy ? (
        <>
          <ActivityIndicator color={theme.c.primary} />
          {progress > 0 ? (
            <AppText variant="caption" color="textTertiary">
              {progress}%
            </AppText>
          ) : null}
        </>
      ) : (
        <>
          <MaterialCommunityIcons name={icon} size={22} color={theme.c.textSecondary} />
          <AppText variant="caption" color="textSecondary">
            {label}
          </AppText>
        </>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  tile: {
    width: 88,
    height: 88,
  },
  addTile: {
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    borderWidth: 1,
    borderStyle: 'dashed',
  },
  deleteBadge: {
    position: 'absolute',
    top: -6,
    right: -6,
    width: 22,
    height: 22,
    borderRadius: 11,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
