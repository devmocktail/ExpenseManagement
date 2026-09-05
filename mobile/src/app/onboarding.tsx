import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import {
  FlatList,
  Pressable,
  StyleSheet,
  useWindowDimensions,
  View,
  type NativeScrollEvent,
  type NativeSyntheticEvent,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppButton } from '@/components/ui/AppButton';
import { AppText } from '@/components/ui/AppText';
import { usePreferencesStore } from '@/store/preferences-store';
import { useAppTheme } from '@/theme/ThemeProvider';

type Slide = {
  key: string;
  icon: React.ComponentProps<typeof MaterialCommunityIcons>['name'];
  title: string;
  description: string;
};

const SLIDES: Slide[] = [
  {
    key: 'track',
    icon: 'notebook-outline',
    title: 'Track your money',
    description:
      'Record an expense in seconds. Amount, category, done — the details are there when you want them and out of the way when you do not.',
  },
  {
    key: 'understand',
    icon: 'chart-donut',
    title: 'Understand your spending',
    description:
      'See where your money actually goes each month, broken down by category, with trends that make the pattern obvious.',
  },
  {
    key: 'goals',
    icon: 'target',
    title: 'Reach your goals',
    description:
      'Set a budget and get told before you overspend, not after. Small nudges, no nagging.',
  },
];

/**
 * Three-screen introduction.
 *
 * Skippable from the first slide, because someone who has used a budgeting app
 * before does not need the tour and making them swipe through it is friction on
 * their very first impression. Completion persists locally, so it never runs
 * twice on the same device — including after a sign-out.
 */
export default function OnboardingScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();

  const listRef = useRef<FlatList<Slide>>(null);
  const [index, setIndex] = useState(0);

  const completeOnboarding = usePreferencesStore((s) => s.completeOnboarding);

  const finish = () => {
    completeOnboarding();
    router.replace('/(auth)/login');
  };

  const next = () => {
    if (index >= SLIDES.length - 1) {
      finish();
      return;
    }

    const target = index + 1;
    setIndex(target);
    listRef.current?.scrollToIndex({ index: target, animated: true });
  };

  // Derived from the scroll offset rather than onViewableItemsChanged, which
  // fires inconsistently mid-swipe and makes the dots flicker.
  const onMomentumEnd = (event: NativeSyntheticEvent<NativeScrollEvent>) => {
    setIndex(Math.round(event.nativeEvent.contentOffset.x / width));
  };

  return (
    <View
      style={[
        styles.flex,
        { backgroundColor: theme.c.background, paddingTop: insets.top, paddingBottom: insets.bottom },
      ]}
    >
      <View style={[styles.topBar, { paddingHorizontal: theme.spacing.base }]}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Skip the introduction"
          onPress={finish}
          hitSlop={12}
        >
          <AppText variant="bodySmallStrong" color="textSecondary">
            Skip
          </AppText>
        </Pressable>
      </View>

      <FlatList
        ref={listRef}
        data={SLIDES}
        keyExtractor={(item) => item.key}
        horizontal
        pagingEnabled
        showsHorizontalScrollIndicator={false}
        onMomentumScrollEnd={onMomentumEnd}
        // Required for scrollToIndex to work without measuring every item first.
        getItemLayout={(_data, i) => ({ length: width, offset: width * i, index: i })}
        renderItem={({ item }) => (
          <View style={[styles.slide, { width, paddingHorizontal: theme.spacing.xl }]}>
            <View
              style={{
                width: 140,
                height: 140,
                borderRadius: 70,
                backgroundColor: theme.c.primaryMuted,
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: theme.spacing.xxl,
              }}
            >
              <MaterialCommunityIcons name={item.icon} size={64} color={theme.c.primary} />
            </View>

            <AppText variant="heading1" align="center">
              {item.title}
            </AppText>

            <AppText
              variant="body"
              color="textSecondary"
              align="center"
              style={{ marginTop: theme.spacing.md, maxWidth: 340 }}
            >
              {item.description}
            </AppText>
          </View>
        )}
      />

      <View style={{ paddingHorizontal: theme.spacing.lg, gap: theme.spacing.lg }}>
        <View
          style={styles.dots}
          accessibilityRole="progressbar"
          accessibilityLabel={`Step ${index + 1} of ${SLIDES.length}`}
        >
          {SLIDES.map((slide, i) => (
            <View
              key={slide.key}
              style={{
                height: 6,
                // The active dot widens rather than only changing colour, so
                // progress is visible without relying on colour perception.
                width: i === index ? 22 : 6,
                borderRadius: 3,
                backgroundColor: i === index ? theme.c.primary : theme.c.border,
              }}
            />
          ))}
        </View>

        <AppButton
          label={index === SLIDES.length - 1 ? 'Get started' : 'Next'}
          size="large"
          fullWidth
          onPress={next}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  topBar: {
    height: 44,
    alignItems: 'flex-end',
    justifyContent: 'center',
  },
  slide: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dots: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
  },
});
